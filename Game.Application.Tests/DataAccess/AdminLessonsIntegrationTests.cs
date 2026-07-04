using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Exercises <see cref="IAdminLessons"/> (#1591, spike #1392): the retire-only identity save, the trigger
    /// consistency guards (ScreenVisit vs. MechanicEvent), the live-key uniqueness guard, and the wholesale
    /// step-list replacement. Seed, write, and assert each use a separate DI scope, as a real admin call does.
    /// </summary>
    [Collection("Integration")]
    public class AdminLessonsIntegrationTests : ApplicationIntegrationTestBase
    {
        public AdminLessonsIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task SaveLessons_AddsAScreenVisitLesson()
        {
            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminLessons>();
                var result = admin.SaveLessons(
                [
                    new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = NewScreenVisitLesson(key: "first-visit") },
                ]);
                Assert.True(result.Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Contains(await context.Lessons.ToListAsync(CancellationToken), l => l.Key == "first-visit");
        }

        [Fact]
        public async Task SaveLessons_AddsAMechanicEventLesson()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var result = admin.SaveLessons(
            [
                new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = NewMechanicEventLesson(key: "first-crit", EMechanicEvent.FirstCrit) },
            ]);

            Assert.True(result.Succeeded);
        }

        [Fact]
        public async Task SaveLessons_ScreenVisitMissingTriggerScreenKey_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var lesson = NewScreenVisitLesson(key: "bad");
            lesson.TriggerScreenKey = null;

            var result = admin.SaveLessons([new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = lesson }]);

            Assert.False(result.Succeeded);
            Assert.Contains("must declare a TriggerScreenKey", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveLessons_ScreenVisitCarryingMechanicEvent_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var lesson = NewScreenVisitLesson(key: "bad");
            lesson.TriggerMechanicEvent = EMechanicEvent.FirstCrit;

            var result = admin.SaveLessons([new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = lesson }]);

            Assert.False(result.Succeeded);
            Assert.Contains("must not declare a TriggerMechanicEvent", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveLessons_MechanicEventMissingTrigger_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var lesson = NewMechanicEventLesson(key: "bad", EMechanicEvent.FirstCrit);
            lesson.TriggerMechanicEvent = null;

            var result = admin.SaveLessons([new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = lesson }]);

            Assert.False(result.Succeeded);
            Assert.Contains("must declare a valid TriggerMechanicEvent", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveLessons_MechanicEventInvalidValue_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var lesson = NewMechanicEventLesson(key: "bad", (EMechanicEvent)9999);

            var result = admin.SaveLessons([new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = lesson }]);

            Assert.False(result.Succeeded);
            Assert.Contains("must declare a valid TriggerMechanicEvent", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveLessons_MechanicEventCarryingTriggerScreenKey_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var lesson = NewMechanicEventLesson(key: "bad", EMechanicEvent.FirstCrit);
            lesson.TriggerScreenKey = "fight-screen";

            var result = admin.SaveLessons([new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = lesson }]);

            Assert.False(result.Succeeded);
            Assert.Contains("must not declare a TriggerScreenKey", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveLessons_EmptyHostScreenKey_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var lesson = NewScreenVisitLesson(key: "bad");
            lesson.HostScreenKey = "";

            var result = admin.SaveLessons([new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = lesson }]);

            Assert.False(result.Succeeded);
            Assert.Contains("must declare a non-empty HostScreenKey", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveLessons_DuplicateKeyWithinBatch_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var result = admin.SaveLessons(
            [
                new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = NewScreenVisitLesson(key: "dup") },
                new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = NewScreenVisitLesson(key: "dup") },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("already used by another live lesson", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveLessons_DuplicateKeyAgainstExistingLiveLesson_ReturnsFailure()
        {
            using (var seedScope = CreateScope())
            {
                await SeedLessonAsync(seedScope, key: "existing");
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var result = admin.SaveLessons(
            [
                new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = NewScreenVisitLesson(key: "existing") },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("already used by another live lesson", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveLessons_DuplicateKeyButExistingIsRetired_Succeeds()
        {
            int lessonId;
            using (var seedScope = CreateScope())
            {
                lessonId = (await SeedLessonAsync(seedScope, key: "reused", retired: true)).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var result = admin.SaveLessons(
            [
                new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = NewScreenVisitLesson(key: "reused") },
            ]);

            Assert.True(result.Succeeded);
        }

        [Fact]
        public async Task SaveLessons_Delete_ReturnsRetiredNotDeleted()
        {
            int lessonId;
            using (var seedScope = CreateScope())
            {
                lessonId = (await SeedLessonAsync(seedScope, key: "to-delete")).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var result = admin.SaveLessons(
            [
                new Change<Contracts.Lesson> { ChangeType = EChangeType.Delete, Item = NewScreenVisitLesson(id: lessonId, key: "to-delete") },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("retired, not deleted", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveLessons_EditOutOfRangeId_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var result = admin.SaveLessons(
            [
                new Change<Contracts.Lesson> { ChangeType = EChangeType.Edit, Item = NewScreenVisitLesson(id: 99999, key: "ghost") },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Lesson not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetSteps_EmptyList_ReturnsFailure()
        {
            int lessonId;
            using (var seedScope = CreateScope())
            {
                lessonId = (await SeedLessonAsync(seedScope, key: "steps-empty")).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var result = admin.SetSteps(new SetLessonStepsData { Id = lessonId, Steps = [] });

            Assert.False(result.Succeeded);
            Assert.Contains("at least one step", result.ErrorMessage);
        }

        [Fact]
        public async Task SetSteps_StepWithEmptyText_ReturnsFailure()
        {
            int lessonId;
            using (var seedScope = CreateScope())
            {
                lessonId = (await SeedLessonAsync(seedScope, key: "steps-blank")).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var result = admin.SetSteps(new SetLessonStepsData
            {
                Id = lessonId,
                Steps = [new Contracts.LessonStep { Text = "" }],
            });

            Assert.False(result.Succeeded);
            Assert.Contains("non-empty Text", result.ErrorMessage);
        }

        [Fact]
        public async Task SetSteps_UnknownLesson_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var result = admin.SetSteps(new SetLessonStepsData
            {
                Id = 99999,
                Steps = [new Contracts.LessonStep { Text = "Step" }],
            });

            Assert.False(result.Succeeded);
            Assert.Equal("Lesson not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetSteps_ReplacesExistingStepsWholesale()
        {
            int lessonId;
            using (var seedScope = CreateScope())
            {
                var lesson = await SeedLessonAsync(seedScope, key: "steps-replace");
                lessonId = lesson.Id;

                var seedContext = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                seedContext.LessonSteps.Add(new Entities.LessonStep { LessonId = lessonId, Order = 0, Text = "Old step" });
                await seedContext.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminLessons>();
                var result = admin.SetSteps(new SetLessonStepsData
                {
                    Id = lessonId,
                    Steps =
                    [
                        new Contracts.LessonStep { Text = "New first", AnchorKey = "a" },
                        new Contracts.LessonStep { Text = "New second" },
                    ],
                });
                Assert.True(result.Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var steps = await context.LessonSteps
                .Where(s => s.LessonId == lessonId)
                .OrderBy(s => s.Order)
                .ToListAsync(CancellationToken);

            Assert.Equal(["New first", "New second"], steps.Select(s => s.Text));
            Assert.Equal([0, 1], steps.Select(s => s.Order));
            Assert.Equal("a", steps[0].AnchorKey);
        }

        private async Task<Entities.Lesson> SeedLessonAsync(IServiceScope scope, string key, bool retired = false)
        {
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var lesson = new Entities.Lesson
            {
                Key = key,
                Name = key,
                TriggerType = ELessonTriggerType.ScreenVisit,
                TriggerScreenKey = "fight-screen",
                HostScreenKey = "fight-screen",
                DisplayOrder = 0,
                RetiredAt = retired ? DateTime.UtcNow : null,
            };
            context.Lessons.Add(lesson);
            await context.SaveChangesAsync(CancellationToken);
            return lesson;
        }

        private static Contracts.Lesson NewScreenVisitLesson(string key, int id = 0) => new()
        {
            Id = id,
            Key = key,
            Name = key,
            TriggerType = ELessonTriggerType.ScreenVisit,
            TriggerScreenKey = "fight-screen",
            HostScreenKey = "fight-screen",
            DisplayOrder = 0,
            Steps = [],
        };

        private static Contracts.Lesson NewMechanicEventLesson(string key, EMechanicEvent mechanicEvent, int id = 0) => new()
        {
            Id = id,
            Key = key,
            Name = key,
            TriggerType = ELessonTriggerType.MechanicEvent,
            TriggerMechanicEvent = mechanicEvent,
            HostScreenKey = "fight-screen",
            DisplayOrder = 0,
            Steps = [],
        };
    }
}
