using Game.Core.Attributes.Modifiers;
using Game.Core.Items;

namespace Game.Core.Players.Inventories
{
    /// <summary>
    /// Represents a player's collection of unlocked items and modifiers.
    /// </summary>
    public class Inventory
    {
        private static readonly int EquipSlots = (int)Enum.GetValues<EEquipmentSlot>().Max();

        /// <summary>All items the player has unlocked.</summary>
        public List<UnlockedItemSlot> UnlockedItems { get; set; }

        /// <summary>IDs of all modifiers the player has unlocked.</summary>
        public HashSet<int> UnlockedMods { get; set; }

        /// <summary>The player's equipment slots.</summary>
        public List<EquipmentSlot> EquipmentSlots { get; set; }

        public Inventory()
        {
            UnlockedItems = [];
            UnlockedMods = [];
            EquipmentSlots = NewEquippedList();
        }

        public IEnumerable<AttributeModifier> GetEquippedAttributeModifiers()
        {
            return EquipmentSlots.SelectNotNull(slot => slot.Item)
                .SelectMany(item => item.Attributes
                    .Concat(item.ModSlots
                        .SelectNotNull(mSlot => mSlot.ItemMod)
                        .SelectMany(mod => mod.Attributes)
                    )
                );
        }

        public bool TryEquipItem(int itemId, EEquipmentSlot slot)
        {
            var unlocked = UnlockedItems.FirstOrDefault(u => u.ItemId == itemId);
            if (unlocked is null)
                return false;

            var equipSlot = EquipmentSlots.FirstOrDefault(s => s.Value == slot);
            if (equipSlot is null)
                return false;

            if (equipSlot.ItemCategory != unlocked.Item.Category)
                return false;

            // Unequip from any other slot first
            var currentSlot = EquipmentSlots.FirstOrDefault(s => s.ItemId == itemId);
            if (currentSlot is not null)
            {
                currentSlot.ItemId = null;
                currentSlot.Item = null;
            }

            // Unequip whatever is in the target slot
            if (equipSlot.ItemId.HasValue)
            {
                equipSlot.ItemId = null;
                equipSlot.Item = null;
            }

            equipSlot.ItemId = itemId;
            equipSlot.Item = unlocked.Item;
            return true;
        }

        public bool TryUnequipItem(EEquipmentSlot slot)
        {
            var equipSlot = EquipmentSlots.FirstOrDefault(s => s.Value == slot);
            if (equipSlot is null || !equipSlot.ItemId.HasValue)
                return false;

            equipSlot.ItemId = null;
            equipSlot.Item = null;
            return true;
        }

        public bool TryApplyMod(int itemId, int itemModId, int itemModSlotId, ItemMod mod)
        {
            if (!UnlockedMods.Contains(itemModId))
                return false;

            var unlocked = UnlockedItems.FirstOrDefault(u => u.ItemId == itemId);
            if (unlocked is null)
                return false;

            var modSlot = unlocked.Item.ModSlots.FirstOrDefault(s => s.Index == itemModSlotId);
            if (modSlot is null)
                return false;

            // Verify mod type matches slot type
            if (modSlot.Type != mod.Type)
                return false;

            // Replace any existing mod in the slot
            var existing = unlocked.AppliedMods.FirstOrDefault(a => a.ItemModSlotId == itemModSlotId);
            if (existing is not null)
                unlocked.AppliedMods.Remove(existing);

            unlocked.AppliedMods.Add(new AppliedModSlot
            {
                ItemModId = itemModId,
                ItemModSlotId = itemModSlotId,
                ItemMod = mod,
            });

            return true;
        }

        public bool TryRemoveMod(int itemId, int itemModSlotId)
        {
            var unlocked = UnlockedItems.FirstOrDefault(u => u.ItemId == itemId);
            if (unlocked is null)
                return false;

            var applied = unlocked.AppliedMods.FirstOrDefault(a => a.ItemModSlotId == itemModSlotId);
            if (applied is null)
                return false;

            return unlocked.AppliedMods.Remove(applied);
        }

        public bool TrySetFavorite(int itemId, bool favorite)
        {
            var unlocked = UnlockedItems.FirstOrDefault(u => u.ItemId == itemId);
            if (unlocked is null)
                return false;

            unlocked.Favorite = favorite;
            return true;
        }

        public void UnlockItem(Item item)
        {
            if (UnlockedItems.Any(u => u.ItemId == item.Id))
            {
                return;
            }

            UnlockedItems.Add(new UnlockedItemSlot
            {
                ItemId = item.Id,
                Item = item,
                AppliedMods = [],
            });
        }

        public void UnlockMod(int itemModId)
        {
            UnlockedMods.Add(itemModId);
        }

        private static List<EquipmentSlot> NewEquippedList()
        {
            return Enumerable.Range(0, EquipSlots + 1)
                .Select(index => new EquipmentSlot((EEquipmentSlot)index)
                {
                    Item = null,
                }).ToList();
        }
    }
}
