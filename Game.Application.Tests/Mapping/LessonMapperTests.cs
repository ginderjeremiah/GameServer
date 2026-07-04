using Game.Core;
using Game.DataAccess.Mapping;
using Xunit;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.Mapping
{
    /// <summary>
    /// Coverage for <see cref="LessonMapper"/>: the contract projection round-trips the trigger, host screen,
    /// and ordered steps; the int-backed <see cref="Entities.Lesson.TriggerType"/>/
    /// <see cref="Entities.Lesson.TriggerMechanicEvent"/> columns map to their enums both ways.
    /// </summary>
    public class LessonMapperTests
    {
        [Fact]
        public void ToContract_MechanicEventLesson_RoundTripsTriggerAndOrdersSteps()
        {
            var entity = new Entities.Lesson
            {
                Id = 2,
                Key = "crit-dodge-variance",
                Name = "Crits & Dodges",
                TriggerType = (int)ELessonTriggerType.MechanicEvent,
                ScreenKey = "fight",
                TriggerMechanicEvent = (int)EMechanicEvent.FirstCrit,
                Ordinal = 1,
                DesignerNotes = "designer intent",
                Steps =
                [
                    new Entities.LessonStep { LessonId = 2, Ordinal = 1, Text = "Second", AnchorKey = "b" },
                    new Entities.LessonStep { LessonId = 2, Ordinal = 0, Text = "First", AnchorKey = null },
                ],
            };

            var contract = LessonMapper.ToContract(entity);

            Assert.Equal(2, contract.Id);
            Assert.Equal("crit-dodge-variance", contract.Key);
            Assert.Equal(ELessonTriggerType.MechanicEvent, contract.TriggerType);
            Assert.Equal("fight", contract.ScreenKey);
            Assert.Equal(EMechanicEvent.FirstCrit, contract.TriggerMechanicEvent);
            Assert.Null(contract.RetiredAt);
            Assert.Equal(["First", "Second"], contract.Steps.Select(s => s.Text));
            Assert.Equal([null, "b"], contract.Steps.Select(s => s.AnchorKey));
        }

        [Fact]
        public void ToContract_ScreenVisitLesson_HasNoMechanicEvent()
        {
            var entity = NewLesson(ELessonTriggerType.ScreenVisit, mechanicEvent: null);

            var contract = LessonMapper.ToContract(entity);

            Assert.Equal(ELessonTriggerType.ScreenVisit, contract.TriggerType);
            Assert.Null(contract.TriggerMechanicEvent);
        }

        [Fact]
        public void ToEntity_RoundTripsBackToIntBackedColumns()
        {
            var entity = NewLesson(ELessonTriggerType.MechanicEvent, mechanicEvent: EMechanicEvent.FirstDodge);
            var contract = LessonMapper.ToContract(entity);

            var roundTripped = LessonMapper.ToEntity(contract);

            Assert.Equal((int)ELessonTriggerType.MechanicEvent, roundTripped.TriggerType);
            Assert.Equal((int)EMechanicEvent.FirstDodge, roundTripped.TriggerMechanicEvent);
            Assert.Single(roundTripped.Steps);
        }

        private static Entities.Lesson NewLesson(ELessonTriggerType triggerType, EMechanicEvent? mechanicEvent) => new()
        {
            Id = 0,
            Key = "idle-loop-basics",
            Name = "Idle Combat",
            TriggerType = (int)triggerType,
            ScreenKey = "fight",
            TriggerMechanicEvent = (int?)mechanicEvent,
            Ordinal = 0,
            DesignerNotes = "",
            Steps = [new Entities.LessonStep { LessonId = 0, Ordinal = 0, Text = "Step", AnchorKey = null }],
        };
    }
}
