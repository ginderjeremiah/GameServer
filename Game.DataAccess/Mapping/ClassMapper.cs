using Game.Core;
using Game.Core.Attributes;
using Contracts = Game.Abstractions.Contracts;
using CoreClass = Game.Core.Classes.Class;
using CoreSignaturePassive = Game.Core.Classes.ClassSignaturePassive;
using CoreStarterEquipment = Game.Core.Classes.ClassStarterEquipment;
using EntityClass = Game.Infrastructure.Entities.Class;
using EntityClassStarterSkill = Game.Infrastructure.Entities.ClassStarterSkill;
using EntityClassStarterEquipment = Game.Infrastructure.Entities.ClassStarterEquipment;
using EntityClassAttributeDistribution = Game.Infrastructure.Entities.ClassAttributeDistribution;

namespace Game.DataAccess.Mapping
{
    internal static class ClassMapper
    {
        /// <summary>Maps an entity <see cref="EntityClass"/> (with its child collections loaded) to the
        /// reference-data read <see cref="Contracts.Class"/> contract.</summary>
        public static Contracts.Class ToContract(EntityClass entity)
        {
            return new Contracts.Class
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                Word = entity.Word,
                PassiveAttributeId = (EAttribute)entity.PassiveAttributeId,
                PassiveAmount = entity.PassiveAmount,
                PassiveScalingAttributeId = (EAttribute?)entity.PassiveScalingAttributeId,
                PassiveScalingAmount = entity.PassiveScalingAmount,
                PassiveModifierType = (EModifierType)entity.PassiveModifierType,
                DesignerNotes = entity.DesignerNotes,
                StarterSkillIds = entity.StarterSkills.Select(s => s.SkillId).ToList(),
                StarterEquipment = entity.StarterEquipment
                    .Select(e => new Contracts.ClassStarterEquipment
                    {
                        ItemId = e.ItemId,
                        EquipmentSlot = (EEquipmentSlot)e.EquipmentSlotId,
                    }).ToList(),
                AttributeDistributions = entity.AttributeDistributions
                    .Select(ad => new Contracts.AttributeDistribution
                    {
                        AttributeId = (EAttribute)ad.AttributeId,
                        BaseAmount = ad.BaseAmount,
                        AmountPerLevel = ad.AmountPerLevel,
                    }).ToList(),
                RetiredAt = entity.RetiredAt,
            };
        }

        /// <summary>Maps a reference-data read <see cref="Contracts.Class"/> back to its entity graph (starter
        /// skills, starter equipment, and attribute distributions) for the content seeder.</summary>
        public static EntityClass ToEntity(Contracts.Class contract)
        {
            return new EntityClass
            {
                Id = contract.Id,
                Name = contract.Name,
                Description = contract.Description,
                Word = contract.Word,
                PassiveAttributeId = (int)contract.PassiveAttributeId,
                PassiveAmount = contract.PassiveAmount,
                PassiveScalingAttributeId = (int?)contract.PassiveScalingAttributeId,
                PassiveScalingAmount = contract.PassiveScalingAmount,
                PassiveModifierType = (int)contract.PassiveModifierType,
                DesignerNotes = contract.DesignerNotes,
                RetiredAt = contract.RetiredAt,
                StarterSkills = contract.StarterSkillIds
                    .Select(skillId => new EntityClassStarterSkill
                    {
                        ClassId = contract.Id,
                        SkillId = skillId,
                    }).ToList(),
                StarterEquipment = contract.StarterEquipment
                    .Select(e => new EntityClassStarterEquipment
                    {
                        ClassId = contract.Id,
                        EquipmentSlotId = (int)e.EquipmentSlot,
                        ItemId = e.ItemId,
                    }).ToList(),
                AttributeDistributions = contract.AttributeDistributions
                    .Select(ad => new EntityClassAttributeDistribution
                    {
                        ClassId = contract.Id,
                        AttributeId = (int)ad.AttributeId,
                        BaseAmount = ad.BaseAmount,
                        AmountPerLevel = ad.AmountPerLevel,
                    }).ToList(),
            };
        }

        /// <summary>Maps an entity <see cref="EntityClass"/> (with its child collections loaded) to the lean,
        /// immutable <see cref="CoreClass"/> domain model the gameplay reads share by reference.</summary>
        public static CoreClass ToCore(EntityClass entity)
        {
            return new CoreClass
            {
                Id = entity.Id,
                Name = entity.Name,
                StarterSkillIds = entity.StarterSkills.Select(s => s.SkillId).ToList(),
                StarterEquipment = entity.StarterEquipment
                    .Select(e => new CoreStarterEquipment
                    {
                        ItemId = e.ItemId,
                        EquipmentSlot = (EEquipmentSlot)e.EquipmentSlotId,
                    }).ToList(),
                AttributeDistributions = entity.AttributeDistributions
                    .Select(ad => new AttributeDistribution
                    {
                        AttributeId = (EAttribute)ad.AttributeId,
                        BaseAmount = ad.BaseAmount,
                        AmountPerLevel = ad.AmountPerLevel,
                    }).ToList(),
                SignaturePassive = new CoreSignaturePassive
                {
                    Attribute = (EAttribute)entity.PassiveAttributeId,
                    Amount = entity.PassiveAmount,
                    ScalingAttribute = (EAttribute?)entity.PassiveScalingAttributeId,
                    ScalingAmount = entity.PassiveScalingAmount,
                    ModifierType = (EModifierType)entity.PassiveModifierType,
                },
            };
        }
    }
}
