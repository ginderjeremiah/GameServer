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
    /// Exercises the lesson reference repo against a real database: the contract projection assembles a
    /// lesson's ordered steps, and a retired lesson stays resolvable (via <see cref="ILessons.AllLessons"/>)
    /// rather than disappearing from the set — matching every other reference-data read contract.
    /// </summary>
    [Collection("Integration")]
    public class LessonsIntegrationTests : ApplicationIntegrationTestBase
    {
        public LessonsIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task AllLessons_AssemblesStepsInOrdinalOrder()
        {
            int lessonId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var lesson = new Entities.Lesson
                {
                    Key = "idle-loop-basics",
                    Name = "Idle Combat",
                    TriggerType = (int)ELessonTriggerType.ScreenVisit,
                    ScreenKey = "fight",
                    Ordinal = 0,
                    DesignerNotes = "",
                };
                context.Lessons.Add(lesson);
                await context.SaveChangesAsync(CancellationToken);
                lessonId = lesson.Id;

                context.LessonSteps.AddRange(
                    new Entities.LessonStep { LessonId = lessonId, Ordinal = 1, Text = "Second", AnchorKey = "skill-bar" },
                    new Entities.LessonStep { LessonId = lessonId, Ordinal = 0, Text = "First", AnchorKey = null });
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var lessons = scope.ServiceProvider.GetRequiredService<ILessons>();

            var contract = Assert.Single(lessons.AllLessons(), l => l.Id == lessonId);
            Assert.Equal(ELessonTriggerType.ScreenVisit, contract.TriggerType);
            Assert.Equal(["First", "Second"], contract.Steps.Select(s => s.Text));
        }

        [Fact]
        public async Task AllLessons_IncludesARetiredLesson()
        {
            int lessonId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var lesson = new Entities.Lesson
                {
                    Key = "cooldown-charging",
                    Name = "Cooldowns",
                    TriggerType = (int)ELessonTriggerType.MechanicEvent,
                    TriggerMechanicEvent = (int)EMechanicEvent.FirstCooldownRecharge,
                    ScreenKey = "fight",
                    Ordinal = 0,
                    DesignerNotes = "",
                    RetiredAt = DateTime.UtcNow,
                };
                context.Lessons.Add(lesson);
                await context.SaveChangesAsync(CancellationToken);
                lessonId = lesson.Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var lessons = scope.ServiceProvider.GetRequiredService<ILessons>();

            var contract = Assert.Single(lessons.AllLessons(), l => l.Id == lessonId);
            Assert.NotNull(contract.RetiredAt);
            Assert.Equal(EMechanicEvent.FirstCooldownRecharge, contract.TriggerMechanicEvent);
        }
    }
}
