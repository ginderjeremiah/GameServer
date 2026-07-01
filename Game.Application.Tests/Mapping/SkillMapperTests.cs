using Game.Core;
using Game.DataAccess.Mapping;
using Xunit;
using EntitySkill = Game.Infrastructure.Entities.Skill;
using EntityPortion = Game.Infrastructure.Entities.SkillDamagePortion;

namespace Game.Application.Tests.Mapping
{
    /// <summary>
    /// Coverage for <see cref="SkillMapper"/>: the acquisition bitmask and rarity round-trip from the entity's
    /// stored ints to the contract enums, and the damage portions map to both the contract and the lean core
    /// model (deriving <see cref="Game.Core.Skills.Skill.PrimaryDamageType"/>). These fields are part of the
    /// client-visible contract, so they drive the skills reference-data version hash.
    /// </summary>
    public class SkillMapperTests
    {
        [Theory]
        [InlineData(ESkillAcquisition.None)]
        [InlineData(ESkillAcquisition.Player)]
        [InlineData(ESkillAcquisition.Item)]
        [InlineData(ESkillAcquisition.Enemy)]
        [InlineData(ESkillAcquisition.Player | ESkillAcquisition.Item | ESkillAcquisition.Enemy)]
        public void ToContract_RoundTripsAcquisitionFlags(ESkillAcquisition acquisition)
        {
            var entity = NewSkill(acquisition);

            var contract = SkillMapper.ToContract(entity);

            Assert.Equal(acquisition, contract.Acquisition);
        }

        [Theory]
        [InlineData(ERarity.Common)]
        [InlineData(ERarity.Rare)]
        [InlineData(ERarity.Legendary)]
        [InlineData(ERarity.Mythic)]
        public void ToContract_RoundTripsRarity(ERarity rarity)
        {
            var entity = NewSkill(ESkillAcquisition.Player, rarity);

            var contract = SkillMapper.ToContract(entity);

            Assert.Equal(rarity, contract.RarityId);
        }

        [Fact]
        public void ToContract_MapsDamagePortions()
        {
            var entity = NewSkill(ESkillAcquisition.Player);
            entity.SkillDamagePortions =
            [
                new EntityPortion { SkillId = 0, DamageType = (int)EDamageType.Physical, Weight = 0.6m },
                new EntityPortion { SkillId = 0, DamageType = (int)EDamageType.Fire, Weight = 0.4m },
            ];

            var contract = SkillMapper.ToContract(entity);

            Assert.Collection(contract.DamagePortions,
                p => { Assert.Equal(EDamageType.Physical, p.Type); Assert.Equal(0.6m, p.Weight); },
                p => { Assert.Equal(EDamageType.Fire, p.Type); Assert.Equal(0.4m, p.Weight); });
        }

        [Fact]
        public void ToCore_MapsDamagePortionsAndDerivesPrimaryType()
        {
            var entity = NewSkill(ESkillAcquisition.Player);
            entity.SkillDamagePortions =
            [
                new EntityPortion { SkillId = 0, DamageType = (int)EDamageType.Physical, Weight = 0.4m },
                new EntityPortion { SkillId = 0, DamageType = (int)EDamageType.Fire, Weight = 0.6m },
            ];

            var core = SkillMapper.ToCore(entity);

            Assert.Collection(core.DamagePortions,
                p => { Assert.Equal(EDamageType.Physical, p.Type); Assert.Equal(0.4, p.Weight); },
                p => { Assert.Equal(EDamageType.Fire, p.Type); Assert.Equal(0.6, p.Weight); });
            // The highest-weight portion drives the derived primary type.
            Assert.Equal(EDamageType.Fire, core.PrimaryDamageType);
        }

        [Fact]
        public void ToContract_RoundTripsDesignerNotes()
        {
            var entity = NewSkill(ESkillAcquisition.Player);
            entity.DesignerNotes = "why this skill exists";

            // Authoring-only metadata rides the client-visible contract (like the word of power); the lean
            // Core.Skills.Skill deliberately has no such field, so its absence there is a compile-time guarantee.
            Assert.Equal("why this skill exists", SkillMapper.ToContract(entity).DesignerNotes);
        }

        private static EntitySkill NewSkill(ESkillAcquisition acquisition, ERarity rarity = ERarity.Common) => new()
        {
            Id = 0,
            Name = "Test",
            Description = "",
            IconPath = "",
            Word = "",
            Pronunciation = "",
            Translation = "",
            DesignerNotes = "designer intent",
            BaseDamage = 1m,
            CooldownMs = 1000,
            RarityId = (int)rarity,
            Acquisition = (int)acquisition,
            SkillDamagePortions = [],
            SkillDamageMultipliers = [],
            SkillEffects = [],
        };
    }
}
