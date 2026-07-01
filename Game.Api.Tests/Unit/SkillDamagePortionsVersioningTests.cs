using Game.Abstractions.Contracts;
using Game.Api.Sockets.Commands;
using Game.Core;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Pins that a skill's damage portions are part of the client-visible serialization, so re-typing or
    /// re-weighting a portion changes the skills reference-data version hash and clients re-download the set
    /// (the expected one-time effect of replacing the single damage-type field with portions — spike #1343).
    /// </summary>
    public class SkillDamagePortionsVersioningTests
    {
        [Fact]
        public void ComputeVersion_ChangesWhenAPortionsTypeChanges()
        {
            var baseline = ReferenceDataVersioning.ComputeVersion(
                new[] { NewSkill([new SkillDamagePortion { Type = EDamageType.Physical, Weight = 1m }]) });
            var reTyped = ReferenceDataVersioning.ComputeVersion(
                new[] { NewSkill([new SkillDamagePortion { Type = EDamageType.Fire, Weight = 1m }]) });

            Assert.NotEqual(baseline, reTyped);
        }

        [Fact]
        public void ComputeVersion_ChangesWhenAPortionsWeightChanges()
        {
            var baseline = ReferenceDataVersioning.ComputeVersion(
                new[] { NewSkill([new SkillDamagePortion { Type = EDamageType.Fire, Weight = 1m }]) });
            var reWeighted = ReferenceDataVersioning.ComputeVersion(
                new[] { NewSkill([new SkillDamagePortion { Type = EDamageType.Fire, Weight = 2m }]) });

            Assert.NotEqual(baseline, reWeighted);
        }

        [Fact]
        public void ComputeVersion_ChangesWhenAPortionIsAdded()
        {
            var single = ReferenceDataVersioning.ComputeVersion(
                new[] { NewSkill([new SkillDamagePortion { Type = EDamageType.Physical, Weight = 1m }]) });
            var split = ReferenceDataVersioning.ComputeVersion(
                new[]
                {
                    NewSkill(
                    [
                        new SkillDamagePortion { Type = EDamageType.Physical, Weight = 0.6m },
                        new SkillDamagePortion { Type = EDamageType.Fire, Weight = 0.4m },
                    ]),
                });

            Assert.NotEqual(single, split);
        }

        private static Skill NewSkill(IEnumerable<SkillDamagePortion> portions) => new()
        {
            Id = 0,
            Name = "Test",
            BaseDamage = 1m,
            DamagePortions = portions,
            DamageMultipliers = [],
            Effects = [],
            Description = "",
            DesignerNotes = "",
            IconPath = "",
            RarityId = ERarity.Common,
            Word = "",
            Pronunciation = "",
            Translation = "",
            Acquisition = ESkillAcquisition.Player,
        };
    }
}
