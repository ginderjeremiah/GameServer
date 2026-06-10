using Game.Core;
using Game.Core.Attributes.Modifiers;
using Game.Core.Items;
using Contracts = Game.Abstractions.Contracts;
using EntityItem = Game.Infrastructure.Entities.Item;
using EntityItemMod = Game.Infrastructure.Entities.ItemMod;

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
                Description = entity.Description ?? string.Empty,
                Category = (EItemCategory)entity.ItemCategoryId,
                Rarity = (ERarity)entity.RarityId,
                Attributes = (entity.ItemAttributes ?? [])
                    .Select(ia => new AttributeModifier
                    {
                        Attribute = (EAttribute)ia.AttributeId,
                        Amount = (double)ia.Amount,
                        Type = EModifierType.Additive,
                        Source = EAttributeModifierSource.Item,
                    }).ToList(),
                ModSlots = (entity.ItemModSlots ?? [])
                    .Select(ims => new ItemModSlot
                    {
                        Id = ims.Id,
                        Type = (EItemModType)ims.ItemModSlotTypeId,
                        Index = ims.Index,
                        ItemMod = null,
                    }).ToList(),
                Tags = [],
            };
        }

        public static ItemMod ModToCore(EntityItemMod entity)
        {
            return new ItemMod
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description ?? string.Empty,
                Type = (EItemModType)entity.ItemModTypeId,
                Rarity = (ERarity)entity.RarityId,
                Attributes = (entity.ItemModAttributes ?? [])
                    .Select(ima => new AttributeModifier
                    {
                        Attribute = (EAttribute)ima.AttributeId,
                        Amount = (double)ima.Amount,
                        Type = EModifierType.Additive,
                        Source = EAttributeModifierSource.ItemMod,
                    }).ToList(),
                Tags = [],
            };
        }

        /// <summary>Maps an entity <see cref="EntityItem"/> (with its child collections loaded) to the
        /// reference-data read <see cref="Contracts.Item"/> contract.</summary>
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
                Attributes = entity.ItemAttributes
                    .Select(ia => new Contracts.BattlerAttribute
                    {
                        AttributeId = (EAttribute)ia.AttributeId,
                        Amount = ia.Amount,
                    }).ToList(),
                ModSlots = entity.ItemModSlots
                    .Select(ims => new Contracts.ItemModSlot
                    {
                        Id = ims.Id,
                        ItemId = ims.ItemId,
                        ItemModSlotTypeId = (EItemModType)ims.ItemModSlotTypeId,
                    }).ToList(),
                Tags = entity.Tags.Select(t => t.Id).ToList(),
                RetiredAt = entity.RetiredAt,
            };
        }

        /// <summary>Maps an entity <see cref="EntityItemMod"/> (with its child collections loaded) to the
        /// reference-data read <see cref="Contracts.ItemMod"/> contract.</summary>
        public static Contracts.ItemMod ModToContract(EntityItemMod entity)
        {
            return new Contracts.ItemMod
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                ItemModTypeId = (EItemModType)entity.ItemModTypeId,
                RarityId = (ERarity)entity.RarityId,
                Attributes = entity.ItemModAttributes
                    .Select(ima => new Contracts.BattlerAttribute
                    {
                        AttributeId = (EAttribute)ima.AttributeId,
                        Amount = ima.Amount,
                    }).ToList(),
                Tags = entity.Tags.Select(t => t.Id).ToList(),
                RetiredAt = entity.RetiredAt,
            };
        }
    }
}
