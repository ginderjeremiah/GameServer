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
    /// Exercises <see cref="IAdminLessons"/>: the retire-only identity save, the trigger-consistency guard,
    /// the Key-uniqueness guard, and the ordered-steps reconciler. Seed, write, and assert each use a separate
    /// DI scope, as a real admin call does.
    /// </summary>
    [Collection("Integration")]
    public class AdminLessonsIntegrationTests : ApplicationIntegrationTestBase
    {
        public AdminLessonsIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task SaveLessons_AddsALesson()
        {
            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminLessons>();
                Assert.True(admin.SaveLessons(
                [
                    new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = NewLesson(key: "idle-loop-basics") },
                ]).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Contains(await context.Lessons.ToListAsync(CancellationToken), l => l.Key == "idle-loop-basics");
        }

        [Fact]
        public async Task SaveLessons_MechanicEventLessonWithNoEvent_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var lesson = NewLesson(key: "crit-dodge-variance", triggerType: ELessonTriggerType.MechanicEvent, mechanicEvent: null);
            var result = admin.SaveLessons([new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = lesson }]);

            Assert.False(result.Succeeded);
            Assert.Contains("must name a mechanic event", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveLessons_ScreenVisitLessonWithMechanicEvent_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var lesson = NewLesson(key: "idle-loop-basics", triggerType: ELessonTriggerType.ScreenVisit, mechanicEvent: EMechanicEvent.FirstCrit);
            var result = admin.SaveLessons([new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = lesson }]);

            Assert.False(result.Succeeded);
            Assert.Contains("cannot also carry a mechanic-event trigger", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveLessons_DuplicateKeyWithinBatch_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var result = admin.SaveLessons(
            [
                new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = NewLesson(key: "idle-loop-basics") },
                new Change<Contracts.Lesson> { ChangeType = EChangeType.Add, Item = NewLesson(key: "idle-loop-basics") },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("share the key 'idle-loop-basics'", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveLessons_EditKeyCollidingWithExistingLesson_ReturnsFailure()
        {
            int otherLessonId;
            using (var seedScope = CreateScope())
            {
                otherLessonId = (await SeedLessonAsync(seedScope, key: "cooldown-charging")).Id;
                await SeedLessonAsync(seedScope, key: "idle-loop-basics");
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var result = admin.SaveLessons(
            [
                new Change<Contracts.Lesson> { ChangeType = EChangeType.Edit, Item = NewLesson(id: otherLessonId, key: "idle-loop-basics") },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("share the key 'idle-loop-basics'", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveLessons_Delete_ReturnsRetiredNotDeleted()
        {
            int lessonId;
            using (var seedScope = CreateScope())
            {
                lessonId = (await SeedLessonAsync(seedScope, key: "idle-loop-basics")).Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var result = admin.SaveLessons(
            [
                new Change<Contracts.Lesson> { ChangeType = EChangeType.Delete, Item = NewLesson(id: lessonId) },
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
                new Change<Contracts.Lesson> { ChangeType = EChangeType.Edit, Item = NewLesson(id: 99999, key: "idle-loop-basics") },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Lesson not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SetSteps_ReconcilesSteps()
        {
            int lessonId;
            using (var seedScope = CreateScope())
            {
                lessonId = (await SeedLessonAsync(seedScope, key: "idle-loop-basics")).Id;
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
                        new Contracts.LessonStep { Ordinal = 0, Text = "First", AnchorKey = "skill-bar" },
                        new Contracts.LessonStep { Ordinal = 1, Text = "Second", AnchorKey = null },
                    ],
                });
                Assert.True(result.Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using var assertScope = CreateScope();
            var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var steps = await context.LessonSteps.Where(s => s.LessonId == lessonId).OrderBy(s => s.Ordinal).ToListAsync(CancellationToken);
            Assert.Equal(["First", "Second"], steps.Select(s => s.Text));
            Assert.Equal("skill-bar", steps[0].AnchorKey);
        }

        [Fact]
        public async Task SetSteps_UnknownLesson_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminLessons>();

            var result = admin.SetSteps(new SetLessonStepsData { Id = 99999, Steps = [] });

            Assert.False(result.Succeeded);
            Assert.Equal("Lesson not found.", result.ErrorMessage);
        }

        private async Task<Entities.Lesson> SeedLessonAsync(IServiceScope scope, string key)
        {
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var lesson = new Entities.Lesson
            {
                Key = key,
                Name = "Lesson",
                TriggerType = (int)ELessonTriggerType.ScreenVisit,
                ScreenKey = "fight",
                Ordinal = 0,
                DesignerNotes = "",
            };
            context.Lessons.Add(lesson);
            await context.SaveChangesAsync(CancellationToken);
            return lesson;
        }

        private static Contracts.Lesson NewLesson(
            int id = 0,
            string key = "idle-loop-basics",
            ELessonTriggerType triggerType = ELessonTriggerType.ScreenVisit,
            EMechanicEvent? mechanicEvent = null) => new()
            {
                Id = id,
                Key = key,
                Name = "Lesson",
                TriggerType = triggerType,
                ScreenKey = "fight",
                TriggerMechanicEvent = mechanicEvent,
                Ordinal = 0,
                DesignerNotes = "",
                Steps = [],
            };
    }
}
