using Game.Abstractions.Contracts;
using Game.Api.Sockets.Commands;
using Game.Core;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Pins that a skill's acquisition classification is part of the client-visible serialization, so
    /// re-flagging a skill changes the skills reference-data version hash and clients re-download the set
    /// once (the expected one-time effect of introducing the field).
    /// </summary>
    public class SkillAcquisitionVersioningTests
    {
        [Fact]
        public void ComputeVersion_ChangesWhenAcquisitionFlagsChange()
        {
            var baseline = ReferenceDataVersioning.ComputeVersion(new[] { NewSkill(ESkillAcquisition.Player) });
            var reflagged = ReferenceDataVersioning.ComputeVersion(
                new[] { NewSkill(ESkillAcquisition.Player | ESkillAcquisition.Item) });

            Assert.NotEqual(baseline, reflagged);
        }

        private static Skill NewSkill(ESkillAcquisition acquisition) => new()
        {
            Id = 0,
            Name = "Test",
            BaseDamage = 1m,
            DamageMultipliers = [],
            Effects = [],
            DamagePortions = [new SkillDamagePortion { Type = EDamageType.Physical, Weight = 1m }],
            Description = "",
            IconPath = "",
            Word = "",
            Pronunciation = "",
            Translation = "",
            Acquisition = acquisition,
        };
    }
}
