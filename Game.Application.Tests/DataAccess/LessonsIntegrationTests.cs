using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Exercises the lesson reference repo against a real database (#1591, spike #1392): the contract
    /// projection assembles a lesson's ordered steps and both trigger kinds, and a retired lesson stays
    /// resolvable but is excluded from the live catalogue the client is expected to act on.
    /// </summary>
    [Collection("Integration")]
    public class LessonsIntegrationTests : ApplicationIntegrationTestBase
    {
        public LessonsIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task AllLessons_AssemblesOrderedSteps_ForScreenVisitTrigger()
        {
            int lessonId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();

                var lesson = new Entities.Lesson
                {
                    Key = "first-visit-fight",
                    Name = "Welcome to the Fight Screen",
                    TriggerType = ELessonTriggerType.ScreenVisit,
                    TriggerScreenKey = "fight-screen",
                    HostScreenKey = "fight-screen",
                    DisplayOrder = 0,
                };
                context.Lessons.Add(lesson);
                await context.SaveChangesAsync(CancellationToken);
                lessonId = lesson.Id;

                context.LessonSteps.AddRange(
                    new Entities.LessonStep { LessonId = lessonId, Order = 1, Text = "Second step" },
                    new Entities.LessonStep { LessonId = lessonId, Order = 0, Text = "First step", AnchorKey = "anchor-a" });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var lessons = scope.ServiceProvider.GetRequiredService<ILessons>();

            var contract = Assert.Single(lessons.AllLessons());
            Assert.Equal(lessonId, contract.Id);
            Assert.Equal("first-visit-fight", contract.Key);
            Assert.Equal(ELessonTriggerType.ScreenVisit, contract.TriggerType);
            Assert.Equal("fight-screen", contract.TriggerScreenKey);
            Assert.Null(contract.TriggerMechanicEvent);
            Assert.Equal(["First step", "Second step"], contract.Steps.Select(s => s.Text));
            Assert.Equal("anchor-a", contract.Steps.First().AnchorKey);
        }

        [Fact]
        public async Task AllLessons_AssemblesMechanicEventTrigger()
        {
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();

                var lesson = new Entities.Lesson
                {
                    Key = "first-crit",
                    Name = "Critical Hits",
                    TriggerType = ELessonTriggerType.MechanicEvent,
                    TriggerMechanicEventId = (int)EMechanicEvent.FirstCrit,
                    HostScreenKey = "fight-screen",
                    DisplayOrder = 0,
                };
                context.Lessons.Add(lesson);
                await context.SaveChangesAsync(CancellationToken);

                context.LessonSteps.Add(new Entities.LessonStep { LessonId = lesson.Id, Order = 0, Text = "Crits deal bonus damage." });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var lessons = scope.ServiceProvider.GetRequiredService<ILessons>();

            var contract = Assert.Single(lessons.AllLessons());
            Assert.Equal(ELessonTriggerType.MechanicEvent, contract.TriggerType);
            Assert.Null(contract.TriggerScreenKey);
            Assert.Equal(EMechanicEvent.FirstCrit, contract.TriggerMechanicEvent);
        }

        [Fact]
        public async Task AllLessons_IncludesRetiredLessons_WithRetiredAtSet()
        {
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();

                var lesson = new Entities.Lesson
                {
                    Key = "retired-lesson",
                    Name = "Retired",
                    TriggerType = ELessonTriggerType.ScreenVisit,
                    TriggerScreenKey = "fight-screen",
                    HostScreenKey = "fight-screen",
                    DisplayOrder = 0,
                    RetiredAt = DateTime.UtcNow,
                };
                context.Lessons.Add(lesson);
                await context.SaveChangesAsync(CancellationToken);

                context.LessonSteps.Add(new Entities.LessonStep { LessonId = lesson.Id, Order = 0, Text = "Step" });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var lessons = scope.ServiceProvider.GetRequiredService<ILessons>();

            // Retired reference records stay resolvable rather than being dropped (docs/backend.md → Reference Data).
            var contract = Assert.Single(lessons.AllLessons());
            Assert.NotNull(contract.RetiredAt);
        }
    }
}
