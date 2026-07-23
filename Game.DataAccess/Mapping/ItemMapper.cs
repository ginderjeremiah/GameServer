using Game.Core;
using Game.Core.Attributes.Modifiers;
using Game.Core.Items;
using Contracts = Game.Abstractions.Contracts;
using EntityItem = Game.Infrastructure.Entities.Item;
using EntityItemMod = Game.Infrastructure.Entities.ItemMod;
using EntityItemAttribute = Game.Infrastructure.Entities.ItemAttribute;
using EntityItemModSlot = Game.Infrastructure.Entities.ItemModSlot;
using EntityItemModAttribute = Game.Infrastructure.Entities.ItemModAttribute;

namespace Game.DataAccess.Mapping
{
    internal static class ItemMapper
    {
        public static Item ToCore(EntityItem entity)
        {
            return new Item
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                Category = (EItemCategory)entity.ItemCategoryId,
                Rarity = (ERarity)entity.RarityId,
                GrantedSkillId = entity.GrantedSkillId,
                WeaponType = (EDamageType?)entity.WeaponType,
                RequiredProficiencyId = entity.RequiredProficiencyId,
                RequiredProficiencyLevel = entity.RequiredProficiencyLevel,
                Attributes = entity.ItemAttributes
                    .OrderBy(ia => ia.AttributeId)
                    .Select(ia => new AttributeModifier
                    {
                        Attribute = (EAttribute)ia.AttributeId,
                        Amount = (double)ia.Amount,
                        Type = EModifierType.Additive,
                        Source = EAttributeModifierSource.Item,
                    }).ToList(),
                ModSlots = entity.ItemModSlots
                    .OrderBy(ims => ims.Id)
                    .Select(ims => new ItemModSlot
                    {
                        Id = ims.Id,
                        Type = (EItemModType)ims.ItemModSlotTypeId,
                    }).ToList(),
            };
        }

        public static ItemMod ModToCore(EntityItemMod entity)
        {
            return new ItemMod
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                Type = (EItemModType)entity.ItemModTypeId,
                Rarity = (ERarity)entity.RarityId,
                Attributes = entity.ItemModAttributes
                    .OrderBy(ima => ima.AttributeId)
                    .Select(ima => new AttributeModifier
                    {
                        Attribute = (EAttribute)ima.AttributeId,
                        Amount = (double)ima.Amount,
                        Type = EModifierType.Additive,
                        Source = EAttributeModifierSource.ItemMod,
                    }).ToList(),
            };
        }

        /// <summary>Maps a reference-data read <see cref="Contracts.Item"/> back to its entity graph (attributes
        /// and mod slots) for the content seeder. Tag assignments are join rows the seeder builds separately
        /// (the entity's <c>Tags</c> is a skip navigation over <c>Tag</c> entities, not the join table).</summary>
        public static EntityItem ToEntity(Contracts.Item contract)
        {
            return new EntityItem
            {
                Id = contract.Id,
                Name = contract.Name,
                Description = contract.Description,
                ItemCategoryId = (int)contract.ItemCategoryId,
                IconPath = contract.IconPath,
                RarityId = (int)contract.RarityId,
                GrantedSkillId = contract.GrantedSkillId,
                WeaponType = (int?)contract.WeaponType,
                RequiredProficiencyId = contract.RequiredProficiencyId,
                RequiredProficiencyLevel = contract.RequiredProficiencyLevel,
                DesignerNotes = contract.DesignerNotes,
                RetiredAt = contract.RetiredAt,
                ItemAttributes = contract.Attributes
                    .Select(a => new EntityItemAttribute
                    {
                        ItemId = contract.Id,
                        AttributeId = (int)a.AttributeId,
                        Amount = a.Amount,
                    }).ToList(),
                ItemModSlots = contract.ModSlots
                    .Select(s => new EntityItemModSlot
                    {
                        Id = s.Id,
                        ItemId = contract.Id,
                        ItemModSlotTypeId = (int)s.ItemModSlotTypeId,
                    }).ToList(),
            };
        }

        /// <summary>Maps a reference-data read <see cref="Contracts.ItemMod"/> back to its entity graph
        /// (attributes) for the content seeder. Tag assignments are join rows the seeder builds separately.</summary>
        public static EntityItemMod ModToEntity(Contracts.ItemMod contract)
        {
            return new EntityItemMod
            {
                Id = contract.Id,
                Name = contract.Name,
                Description = contract.Description,
                ItemModTypeId = (int)contract.ItemModTypeId,
                RarityId = (int)contract.RarityId,
                DesignerNotes = contract.DesignerNotes,
                RetiredAt = contract.RetiredAt,
                ItemModAttributes = contract.Attributes
                    .Select(a => new EntityItemModAttribute
                    {
                        ItemModId = contract.Id,
                        AttributeId = (int)a.AttributeId,
                        Amount = a.Amount,
                    }).ToList(),
            };
        }

        /// <summary>Maps an entity <see cref="EntityItem"/> (with its child collections loaded) to the
        /// reference-data read <see cref="Contracts.Item"/> contract. Child collections are ordered
        /// deterministically so the reference set's version hash is stable across reloads.</summary>
        public static Contracts.Item ToContract(EntityItem entity)
        {
            return new Contracts.Item
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                ItemCategoryId = (EItemCategory)entity.ItemCategoryId,
                RarityId = (ERarity)entity.RarityId,
                IconPath = entity.IconPath,
                GrantedSkillId = entity.GrantedSkillId,
                WeaponType = (EDamageType?)entity.WeaponType,
                RequiredProficiencyId = entity.RequiredProficiencyId,
                RequiredProficiencyLevel = entity.RequiredProficiencyLevel,
                DesignerNotes = entity.DesignerNotes,
                Attributes = entity.ItemAttributes
                    .OrderBy(ia => ia.AttributeId)
                    .Select(ia => new Contracts.BattlerAttribute
                    {
                        AttributeId = (EAttribute)ia.AttributeId,
                        Amount = ia.Amount,
                    }).ToList(),
                ModSlots = entity.ItemModSlots
                    .OrderBy(ims => ims.Id)
                    .Select(ims => new Contracts.ItemModSlot
                    {
                        Id = ims.Id,
                        ItemId = ims.ItemId,
                        ItemModSlotTypeId = (EItemModType)ims.ItemModSlotTypeId,
                    }).ToList(),
                Tags = entity.Tags.OrderBy(t => t.Id).Select(t => t.Id).ToList(),
                RetiredAt = entity.RetiredAt,
            };
        }

        /// <summary>Maps an entity <see cref="EntityItemMod"/> (with its child collections loaded) to the
        /// reference-data read <see cref="Contracts.ItemMod"/> contract. Child collections are ordered
        /// deterministically so the reference set's version hash is stable across reloads.</summary>
        public static Contracts.ItemMod ModToContract(EntityItemMod entity)
        {
            return new Contracts.ItemMod
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                ItemModTypeId = (EItemModType)entity.ItemModTypeId,
                RarityId = (ERarity)entity.RarityId,
                DesignerNotes = entity.DesignerNotes,
                Attributes = entity.ItemModAttributes
                    .OrderBy(ima => ima.AttributeId)
                    .Select(ima => new Contracts.BattlerAttribute
                    {
                        AttributeId = (EAttribute)ima.AttributeId,
                        Amount = ima.Amount,
                    }).ToList(),
                Tags = entity.Tags.OrderBy(t => t.Id).Select(t => t.Id).ToList(),
                RetiredAt = entity.RetiredAt,
            };
        }
    }
}
