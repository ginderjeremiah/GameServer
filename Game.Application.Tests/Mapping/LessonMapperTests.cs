using Game.Core;
using Game.DataAccess.Mapping;
using Xunit;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.Mapping
{
    /// <summary>
    /// Coverage for <see cref="LessonMapper"/>: the contract round-trips both trigger kinds (screen-visit and
    /// mechanic-event) and its ordered step list — step order is the array position, not a stored column value,
    /// so a round trip through <c>ToEntity</c>/<c>ToContract</c> must preserve it.
    /// </summary>
    public class LessonMapperTests
    {
        [Fact]
        public void ToContract_ScreenVisitTrigger_RoundTripsFieldsAndOrdersSteps()
        {
            var entity = new Entities.Lesson
            {
                Id = 3,
                Key = "first-crit",
                Name = "First Crit",
                TriggerType = ELessonTriggerType.ScreenVisit,
                TriggerScreenKey = "fight-screen",
                TriggerMechanicEventId = null,
                HostScreenKey = "fight-screen",
                DisplayOrder = 2,
                RetiredAt = null,
                Steps =
                [
                    new Entities.LessonStep { LessonId = 3, Order = 1, Text = "Second", AnchorKey = "b" },
                    new Entities.LessonStep { LessonId = 3, Order = 0, Text = "First", AnchorKey = null },
                ],
            };

            var contract = LessonMapper.ToContract(entity);

            Assert.Equal(3, contract.Id);
            Assert.Equal("first-crit", contract.Key);
            Assert.Equal("First Crit", contract.Name);
            Assert.Equal(ELessonTriggerType.ScreenVisit, contract.TriggerType);
            Assert.Equal("fight-screen", contract.TriggerScreenKey);
            Assert.Null(contract.TriggerMechanicEvent);
            Assert.Equal("fight-screen", contract.HostScreenKey);
            Assert.Equal(2, contract.DisplayOrder);
            Assert.Null(contract.RetiredAt);
            // Steps ordered by the entity's Order column, not construction order.
            Assert.Equal(["First", "Second"], contract.Steps.Select(s => s.Text));
            Assert.Equal([null, "b"], contract.Steps.Select(s => s.AnchorKey));
        }

        [Fact]
        public void ToContract_MechanicEventTrigger_MapsIdToEnum()
        {
            var entity = new Entities.Lesson
            {
                Id = 0,
                Key = "first-dodge",
                Name = "First Dodge",
                TriggerType = ELessonTriggerType.MechanicEvent,
                TriggerScreenKey = null,
                TriggerMechanicEventId = (int)EMechanicEvent.FirstDodge,
                HostScreenKey = "fight-screen",
                DisplayOrder = 0,
                Steps = [new Entities.LessonStep { LessonId = 0, Order = 0, Text = "Step" }],
            };

            var contract = LessonMapper.ToContract(entity);

            Assert.Equal(ELessonTriggerType.MechanicEvent, contract.TriggerType);
            Assert.Null(contract.TriggerScreenKey);
            Assert.Equal(EMechanicEvent.FirstDodge, contract.TriggerMechanicEvent);
        }

        [Fact]
        public void ToEntity_RoundTripsFieldsAndAssignsStepOrderFromArrayPosition()
        {
            var contract = new Contracts.Lesson
            {
                Id = 5,
                Key = "first-cooldown",
                Name = "First Cooldown Recharge",
                TriggerType = ELessonTriggerType.MechanicEvent,
                TriggerScreenKey = null,
                TriggerMechanicEvent = EMechanicEvent.FirstCooldownRecharge,
                HostScreenKey = "fight-screen",
                DisplayOrder = 1,
                RetiredAt = null,
                Steps =
                [
                    new Contracts.LessonStep { Text = "First", AnchorKey = "anchor-a" },
                    new Contracts.LessonStep { Text = "Second", AnchorKey = null },
                ],
            };

            var entity = LessonMapper.ToEntity(contract);

            Assert.Equal(5, entity.Id);
            Assert.Equal("first-cooldown", entity.Key);
            Assert.Equal("First Cooldown Recharge", entity.Name);
            Assert.Equal(ELessonTriggerType.MechanicEvent, entity.TriggerType);
            Assert.Null(entity.TriggerScreenKey);
            Assert.Equal((int)EMechanicEvent.FirstCooldownRecharge, entity.TriggerMechanicEventId);
            Assert.Equal("fight-screen", entity.HostScreenKey);
            Assert.Equal(1, entity.DisplayOrder);

            Assert.Collection(entity.Steps,
                step =>
                {
                    Assert.Equal(0, step.Order);
                    Assert.Equal("First", step.Text);
                    Assert.Equal("anchor-a", step.AnchorKey);
                    Assert.Equal(5, step.LessonId);
                },
                step =>
                {
                    Assert.Equal(1, step.Order);
                    Assert.Equal("Second", step.Text);
                    Assert.Null(step.AnchorKey);
                    Assert.Equal(5, step.LessonId);
                });
        }
    }
}
