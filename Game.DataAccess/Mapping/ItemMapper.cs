using Game.Core;
using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Items;
using EntityInventoryItemMod = Game.Abstractions.Entities.InventoryItemMod;
using EntityItem = Game.Abstractions.Entities.Item;
using EntityItemMod = Game.Abstractions.Entities.ItemMod;

namespace Game.DataAccess.Mapping
{
    internal static class ItemMapper
    {
        /// <summary>
        /// Maps an entity <see cref="EntityItem"/> to a domain <see cref="Item"/>.
        /// Optionally populates the installed mod on each <see cref="ItemModSlot"/> using
        /// <paramref name="inventoryItemMods"/> (the mods linked to a specific inventory record).
        /// </summary>
        public static Item ToCore(EntityItem entity, IEnumerable<EntityInventoryItemMod>? inventoryItemMods = null)
        {
            // Key: ItemModSlotId → installed ItemMod entity
            var installedMods = inventoryItemMods?
                .Where(m => m.ItemMod is not null)
                .ToDictionary(m => m.ItemModSlotId, m => m.ItemMod)
                ?? [];

            return new Item
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description ?? string.Empty,
                Category = (EItemCategory)entity.ItemCategoryId,
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
                        Type = (EItemModType)ims.ItemModSlotTypeId,
                        Probability = ims.Probability,
                        Index = ims.Index,
                        PossibleItemMods = [],   // populated in Phase 3
                        ItemMod = installedMods.TryGetValue(ims.Id, out var mod)
                            ? ModToCore(mod)
                            : null,
                    }).ToList(),
                Tags = [],  // populated in Phase 3
            };
        }

        private static ItemMod ModToCore(EntityItemMod entity)
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
                        Source = EAttributeModifierSource.Item,
                    }).ToList(),
                Tags = [],
            };
        }
    }
}
