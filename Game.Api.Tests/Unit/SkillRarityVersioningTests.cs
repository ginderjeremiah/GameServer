using Game.Abstractions.Contracts;
using Game.Api.Sockets.Commands;
using Game.Core;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Pins that a skill's rarity tier is part of the client-visible serialization, so re-tiering a skill
    /// changes the skills reference-data version hash and clients re-download the set once (the expected
    /// one-time effect of introducing the field).
    /// </summary>
    public class SkillRarityVersioningTests
    {
        [Fact]
        public void ComputeVersion_ChangesWhenRarityChanges()
        {
            var baseline = ReferenceDataVersioning.ComputeVersion(new[] { NewSkill(ERarity.Common) });
            var reTiered = ReferenceDataVersioning.ComputeVersion(new[] { NewSkill(ERarity.Legendary) });

            Assert.NotEqual(baseline, reTiered);
        }

        private static Skill NewSkill(ERarity rarity) => new()
        {
            Id = 0,
            Name = "Test",
            BaseDamage = 1m,
            DamageMultipliers = [],
            Effects = [],
            DamagePortions = [new SkillDamagePortion { Type = EDamageType.Physical, Weight = 1m }],
            Description = "",
            DesignerNotes = "",
            IconPath = "",
            RarityId = rarity,
            Word = "",
            Pronunciation = "",
            Translation = "",
            Acquisition = ESkillAcquisition.Player,
        };
    }
}
