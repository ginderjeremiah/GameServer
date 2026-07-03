using Game.Core.Items;

namespace Game.Core.Players.Inventories
{
    /// <summary>
    /// Represents a player's collection of unlocked items and modifiers.
    /// </summary>
    public class Inventory
    {
        /// <summary>
        /// All unlocked items indexed by <see cref="UnlockedItemSlot.ItemId"/>. The index is the backing
        /// store for the public <see cref="UnlockedItems"/> view, so every per-item lookup
        /// (<see cref="GetUnlockedItem"/> and the Try* methods below) is O(1) rather than a linear scan —
        /// the unlocked set grows unbounded as a player progresses and is read on the battle-start hot path.
        /// </summary>
        private readonly Dictionary<int, UnlockedItemSlot> _unlockedItems = [];

        /// <summary>
        /// All items the player has unlocked. The setter rebuilds the id-keyed index, keeping the aggregate
        /// round-trippable through the player cache's JSON serialization; the getter is a read-only view, so
        /// unlocked items can only be added through the inventory's domain methods.
        /// </summary>
        public IReadOnlyCollection<UnlockedItemSlot> UnlockedItems
        {
            get => _unlockedItems.Values;
            set
            {
                _unlockedItems.Clear();
                foreach (var slot in value)
                {
                    _unlockedItems[slot.ItemId] = slot;
                }
            }
        }

        /// <summary>IDs of all modifiers the player has unlocked.</summary>
        public HashSet<int> UnlockedMods { get; set; }

        /// <summary>The player's equipment slots.</summary>
        public List<EquipmentSlot> EquipmentSlots { get; set; }

        public Inventory()
        {
            UnlockedMods = [];
            EquipmentSlots = NewEquippedList();
        }

        /// <summary>
        /// Returns the unlocked-item slot for <paramref name="itemId"/>, or null if the player has not
        /// unlocked that item. O(1) via the id-keyed index.
        /// </summary>
        public UnlockedItemSlot? GetUnlockedItem(int itemId)
        {
            return _unlockedItems.GetValueOrDefault(itemId);
        }

        public bool TryEquipItem(int itemId, EEquipmentSlot slot, IReadOnlyDictionary<int, int> proficiencyLevels)
        {
            var unlocked = GetUnlockedItem(itemId);
            if (unlocked is null)
            {
                return false;
            }

            var equipSlot = EquipmentSlots.FirstOrDefault(s => s.Value == slot);
            if (equipSlot is null)
            {
                return false;
            }

            if (equipSlot.ItemCategory != unlocked.Item.Category)
            {
                return false;
            }

            // Anti-cheat: gear gated behind a proficiency can only be equipped once the player has reached the
            // required level (a tampered client cannot bypass the frontend gate).
            if (!unlocked.Item.MeetsProficiencyRequirement(proficiencyLevels))
            {
                return false;
            }

            // Unequip from any other slot first
            var currentSlot = EquipmentSlots.FirstOrDefault(s => s.ItemId == itemId);
            currentSlot?.Clear();

            // Equipping overwrites whatever already occupies the target slot.
            equipSlot.Set(unlocked.Item);
            return true;
        }

        /// <summary>
        /// Clears the equipment slot for <paramref name="slot"/>, returning the id of the item that was
        /// unequipped, or null when the slot was already empty (a no-op).
        /// </summary>
        public int? TryUnequipItem(EEquipmentSlot slot)
        {
            var equipSlot = EquipmentSlots.FirstOrDefault(s => s.Value == slot);
            if (equipSlot?.ItemId is not int itemId)
            {
                return null;
            }

            equipSlot.Clear();
            return itemId;
        }

        public bool TryApplyMod(int itemId, int itemModId, int itemModSlotId, ItemMod mod)
        {
            if (!UnlockedMods.Contains(itemModId))
            {
                return false;
            }

            var unlocked = GetUnlockedItem(itemId);
            if (unlocked is null)
            {
                return false;
            }

            var modSlot = unlocked.Item.ModSlots.FirstOrDefault(s => s.Id == itemModSlotId);
            if (modSlot is null)
            {
                return false;
            }

            // Verify mod type matches slot type
            if (modSlot.Type != mod.Type)
            {
                return false;
            }

            // Replace any existing mod in the slot
            var existing = unlocked.AppliedMods.FirstOrDefault(a => a.ItemModSlotId == itemModSlotId);
            if (existing is not null)
            {
                unlocked.AppliedMods.Remove(existing);
            }

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
            var unlocked = GetUnlockedItem(itemId);
            if (unlocked is null)
            {
                return false;
            }

            var applied = unlocked.AppliedMods.FirstOrDefault(a => a.ItemModSlotId == itemModSlotId);
            if (applied is null)
            {
                return false;
            }

            return unlocked.AppliedMods.Remove(applied);
        }

        public bool TrySetFavorite(int itemId, bool favorite)
        {
            var unlocked = GetUnlockedItem(itemId);
            if (unlocked is null)
            {
                return false;
            }

            unlocked.Favorite = favorite;
            return true;
        }

        /// <summary>
        /// Unlocks <paramref name="item"/> for the player, returning whether it was newly unlocked
        /// (<c>false</c> when the player already owned it) so the caller can skip the no-op event and persist.
        /// </summary>
        public bool UnlockItem(Item item)
        {
            if (_unlockedItems.ContainsKey(item.Id))
            {
                return false;
            }

            _unlockedItems[item.Id] = new UnlockedItemSlot
            {
                Item = item,
                AppliedMods = [],
            };
            return true;
        }

        /// <summary>
        /// Unlocks the modifier with the given <paramref name="itemModId"/>, returning whether it was newly
        /// unlocked (<c>false</c> when the player already owned it).
        /// </summary>
        public bool UnlockMod(int itemModId)
        {
            return UnlockedMods.Add(itemModId);
        }

        private static List<EquipmentSlot> NewEquippedList()
        {
            // Derive one slot per equipment-slot enum value directly, so the slot set tracks the enum
            // without depending on it being contiguous or 0-based.
            return Enum.GetValues<EEquipmentSlot>()
                .Select(slot => new EquipmentSlot(slot)
                {
                    Item = null,
                }).ToList();
        }
    }
}
