using Game.Core;
using Game.DataAccess.Mapping;
using Xunit;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.Mapping
{
    /// <summary>
    /// Coverage for <see cref="ClassMapper"/>: the contract and core projections round-trip the scalar
    /// fields (including the flattened signature passive), the child collections, and the optional
    /// scaling-attribute null case.
    /// </summary>
    public class ClassMapperTests
    {
        [Fact]
        public void ToContract_RoundTripsFieldsAndCollections()
        {
            var entity = NewClass(
                starterSkillIds: [3, 7],
                equipment: [(ItemId: 4, Slot: EEquipmentSlot.WeaponSlot)],
                distributions: [(EAttribute.Strength, 5m, 1m)]);

            var contract = ClassMapper.ToContract(entity);

            Assert.Equal(0, contract.Id);
            Assert.Equal("Warrior", contract.Name);
            Assert.Equal("aenkor", contract.Word);
            Assert.Equal("designer intent", contract.DesignerNotes);
            Assert.Equal(EAttribute.Endurance, contract.PassiveAttributeId);
            Assert.Equal(2m, contract.PassiveAmount);
            Assert.Equal(EAttribute.Strength, contract.PassiveScalingAttributeId);
            Assert.Equal(0.5m, contract.PassiveScalingAmount);
            Assert.Equal(EModifierType.Multiplicative, contract.PassiveModifierType);
            Assert.Equal([3, 7], contract.StarterSkillIds);
            var equipment = Assert.Single(contract.StarterEquipment);
            Assert.Equal(4, equipment.ItemId);
            Assert.Equal(EEquipmentSlot.WeaponSlot, equipment.EquipmentSlot);
            var distribution = Assert.Single(contract.AttributeDistributions);
            Assert.Equal(EAttribute.Strength, distribution.AttributeId);
            Assert.Equal(5m, distribution.BaseAmount);
            Assert.Equal(1m, distribution.AmountPerLevel);
        }

        [Fact]
        public void ToCore_MapsScalarFieldsAndCollections()
        {
            var entity = NewClass(
                starterSkillIds: [9],
                equipment: [(ItemId: 1, Slot: EEquipmentSlot.HelmSlot)],
                distributions: [(EAttribute.Endurance, 4m, 2m)]);

            var core = ClassMapper.ToCore(entity);

            Assert.Equal(0, core.Id);
            Assert.Equal("Warrior", core.Name);
            Assert.Equal([9], core.StarterSkillIds);
            var equipment = Assert.Single(core.StarterEquipment);
            Assert.Equal(1, equipment.ItemId);
            Assert.Equal(EEquipmentSlot.HelmSlot, equipment.EquipmentSlot);
            var distribution = Assert.Single(core.AttributeDistributions);
            Assert.Equal(EAttribute.Endurance, distribution.AttributeId);
            Assert.Equal(4m, distribution.BaseAmount);
            Assert.Equal(2m, distribution.AmountPerLevel);
            Assert.Equal(EAttribute.Endurance, core.SignaturePassive.Attribute);
            Assert.Equal(2m, core.SignaturePassive.Amount);
            Assert.Equal(EAttribute.Strength, core.SignaturePassive.ScalingAttribute);
            Assert.Equal(0.5m, core.SignaturePassive.ScalingAmount);
            Assert.Equal(EModifierType.Multiplicative, core.SignaturePassive.ModifierType);
        }

        [Fact]
        public void Mappers_PreserveNullScalingAttribute()
        {
            var entity = NewClass();
            entity.PassiveScalingAttributeId = null;

            Assert.Null(ClassMapper.ToContract(entity).PassiveScalingAttributeId);
            Assert.Null(ClassMapper.ToCore(entity).SignaturePassive.ScalingAttribute);
        }

        private static Entities.Class NewClass(
            List<int>? starterSkillIds = null,
            List<(int ItemId, EEquipmentSlot Slot)>? equipment = null,
            List<(EAttribute Attribute, decimal Base, decimal PerLevel)>? distributions = null) => new()
            {
                Id = 0,
                Name = "Warrior",
                Description = "A martial discipline.",
                Word = "aenkor",
                PassiveAttributeId = (int)EAttribute.Endurance,
                PassiveAmount = 2m,
                PassiveScalingAttributeId = (int)EAttribute.Strength,
                PassiveScalingAmount = 0.5m,
                PassiveModifierType = (int)EModifierType.Multiplicative,
                DesignerNotes = "designer intent",
                StarterSkills = (starterSkillIds ?? []).Select(id => new Entities.ClassStarterSkill { ClassId = 0, SkillId = id }).ToList(),
                StarterEquipment = (equipment ?? []).Select(e => new Entities.ClassStarterEquipment
                {
                    ClassId = 0,
                    ItemId = e.ItemId,
                    EquipmentSlotId = (int)e.Slot,
                }).ToList(),
                AttributeDistributions = (distributions ?? []).Select(d => new Entities.ClassAttributeDistribution
                {
                    ClassId = 0,
                    AttributeId = (int)d.Attribute,
                    BaseAmount = d.Base,
                    AmountPerLevel = d.PerLevel,
                }).ToList(),
            };
    }
}
