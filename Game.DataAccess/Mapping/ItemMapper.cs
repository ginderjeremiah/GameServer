using Game.Core;
using Game.Core.Attributes.Modifiers;
using Game.Core.Items;
using EntityItem = Game.Abstractions.Entities.Item;
using EntityItemMod = Game.Abstractions.Entities.ItemMod;

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
                Name = entity.Name,
                Removable = entity.Removable,
                Description = entity.Description ?? string.Empty,
                Type = (EItemModType)entity.ItemModTypeId,
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
    }
}
