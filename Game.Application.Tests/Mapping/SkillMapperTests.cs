using Game.Core;
using Game.DataAccess.Mapping;
using Xunit;
using EntitySkill = Game.Infrastructure.Entities.Skill;
using EntityPortion = Game.Infrastructure.Entities.SkillDamagePortion;
using EntityMultiplier = Game.Infrastructure.Entities.SkillDamageMultiplier;
using EntityEffect = Game.Infrastructure.Entities.SkillEffect;

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
        public void ToContract_OrdersChildCollectionsRegardlessOfEntityOrder()
        {
            var entity = NewSkill(ESkillAcquisition.Player);
            // Deliberately shuffled (descending) so a stable ordering isn't a coincidence of insertion order.
            entity.SkillDamagePortions =
            [
                new EntityPortion { SkillId = 0, DamageType = (int)EDamageType.Fire, Weight = 0.4m },
                new EntityPortion { SkillId = 0, DamageType = (int)EDamageType.Physical, Weight = 0.6m },
            ];
            entity.SkillDamageMultipliers =
            [
                new EntityMultiplier { SkillId = 0, AttributeId = (int)EAttribute.Intellect, Multiplier = 1m },
                new EntityMultiplier { SkillId = 0, AttributeId = (int)EAttribute.Strength, Multiplier = 2m },
            ];
            entity.SkillEffects =
            [
                new EntityEffect { Id = 9, SkillId = 0 },
                new EntityEffect { Id = 2, SkillId = 0 },
            ];

            var contract = SkillMapper.ToContract(entity);

            // Ordered by damage type / attribute id / own id for a version hash stable across reloads.
            Assert.Equal([EDamageType.Physical, EDamageType.Fire], contract.DamagePortions.Select(p => p.Type));
            Assert.Equal([EAttribute.Strength, EAttribute.Intellect], contract.DamageMultipliers.Select(m => m.AttributeId));
            Assert.Equal([2, 9], contract.Effects.Select(e => e.Id));
        }

        [Fact]
        public void ToCore_OrdersChildCollectionsRegardlessOfEntityOrder()
        {
            var entity = NewSkill(ESkillAcquisition.Player);
            entity.SkillDamagePortions =
            [
                new EntityPortion { SkillId = 0, DamageType = (int)EDamageType.Fire, Weight = 0.4m },
                new EntityPortion { SkillId = 0, DamageType = (int)EDamageType.Physical, Weight = 0.6m },
            ];
            entity.SkillDamageMultipliers =
            [
                new EntityMultiplier { SkillId = 0, AttributeId = (int)EAttribute.Intellect, Multiplier = 1m },
                new EntityMultiplier { SkillId = 0, AttributeId = (int)EAttribute.Strength, Multiplier = 2m },
            ];
            entity.SkillEffects =
            [
                new EntityEffect { Id = 9, SkillId = 0 },
                new EntityEffect { Id = 2, SkillId = 0 },
            ];

            var core = SkillMapper.ToCore(entity);

            Assert.Equal([EDamageType.Physical, EDamageType.Fire], core.DamagePortions.Select(p => p.Type));
            Assert.Equal([EAttribute.Strength, EAttribute.Intellect], core.DamageMultipliers.Select(m => m.Attribute));
            Assert.Equal([2, 9], core.Effects.Select(e => e.Id));
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

        [Fact]
        public void ToContract_RoundTripsCriticalChance()
        {
            var entity = NewSkill(ESkillAcquisition.Player);
            entity.CriticalChance = 0.25m;

            Assert.Equal(0.25m, SkillMapper.ToContract(entity).CriticalChance);
        }

        [Fact]
        public void ToCore_RoundTripsCriticalChance()
        {
            var entity = NewSkill(ESkillAcquisition.Player);
            entity.CriticalChance = 0.25m;

            Assert.Equal(0.25, SkillMapper.ToCore(entity).CriticalChance);
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
