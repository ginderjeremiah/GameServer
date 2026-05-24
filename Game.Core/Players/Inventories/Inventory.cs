using Game.Core.Attributes.Modifiers;
using Game.Core.Inventories;
using Game.Core.Items;

namespace Game.Core.Players.Inventories
{
    /// <summary>
    /// Represents an <see cref="Item"/> collection.  Some items may be equipped and have effect in battle, others are just in storage.
    /// </summary>
    public class Inventory
    {
        private static readonly int EquipSlots = (int)Enum.GetValues<EEquipmentSlot>().Max();

        /// <summary>All items currently in storage (not equipped).</summary>
        public List<InventorySlot> InventorySlots { get; set; }

        /// <summary>
        /// Slots containing the currently equipped items.
        /// </summary>
        public List<EquipmentSlot> EquipmentSlots { get; set; }

        /// <summary>
        /// Creates a new <see cref="Inventory"/> instance.
        /// </summary>
        public Inventory()
        {
            InventorySlots = [];
            EquipmentSlots = NewEquippedList();
        }

        /// <summary>
        /// Gets an <see cref="AttributeModifier"/> collection based on the currently equipped items.
        /// </summary>
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

        /// <summary>
        /// Gets the slot numbers of each free storage slot in the inventory.
        /// </summary>
        public List<int> GetFreeSlotNumbers()
        {
            return InventorySlots.Where(slot => slot.Item is null).Select(slot => slot.SlotNumber).ToList();
        }

        /// <summary>
        /// Returns the lowest non-negative storage slot number not currently in use.
        /// </summary>
        public int GetNextFreeSlotNumber()
        {
            var usedSlots = new HashSet<int>(InventorySlots.Select(s => s.SlotNumber));
            var freeSlot = 0;
            while (usedSlots.Contains(freeSlot)) freeSlot++;
            return freeSlot;
        }

        /// <summary>
        /// Attempts to apply a batch of slot reassignments atomically.
        /// Validates that no two items share the same destination slot and that all
        /// slot numbers are in range before applying any changes.
        /// </summary>
        /// <returns><c>true</c> if the update was valid and applied; <c>false</c> if any
        /// validation rule was violated (inventory is unchanged in that case).</returns>
        public bool TryUpdateSlots(IEnumerable<IInventoryUpdate> updates)
        {
            // Build a flat lookup of every item currently in inventory by InventoryItemId.
            var allItems = InventorySlots
                .ToDictionary(s => s.InventoryItemId, s => s.Item)
                .Concat(EquipmentSlots
                    .Where(s => s.Item is not null)
                    .ToDictionary(s => s.InventoryItemId, s => s.Item))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            var updateList = updates.ToList();

            // All IDs must belong to items already in this inventory.
            if (updateList.Any(u => !allItems.ContainsKey(u.Id)))
                return false;

            // No two updates may target the same (Equipped, SlotNumber) pair.
            var usedSlots = new HashSet<(bool Equipped, int SlotNumber)>();
            foreach (var update in updateList)
            {
                if (update.SlotNumber < 0)
                    return false;

                if (update.Equipped && !Enum.IsDefined(typeof(EEquipmentSlot), update.SlotNumber))
                    return false;

                if (!usedSlots.Add((update.Equipped, update.SlotNumber)))
                    return false;
            }

            // Validation passed — apply changes.
            InventorySlots = updateList
                .Where(u => !u.Equipped)
                .Select(u => new InventorySlot
                {
                    InventoryItemId = u.Id,
                    SlotNumber = u.SlotNumber,
                    Item = allItems[u.Id],
                }).ToList();

            // Clear then re-populate equipment slots.
            foreach (var slot in EquipmentSlots)
            {
                slot.Item = null;
                slot.InventoryItemId = 0;
            }

            foreach (var update in updateList.Where(u => u.Equipped))
            {
                var eSlot = EquipmentSlots.FirstOrDefault(s => (int)s.Value == update.SlotNumber);
                if (eSlot is not null)
                {
                    eSlot.Item = allItems[update.Id];
                    eSlot.InventoryItemId = update.Id;
                }
            }

            return true;
        }

        /// <summary>
        /// Adds <paramref name="item"/> to the first available storage slot.
        /// </summary>
        /// <param name="item">The domain item to add.</param>
        /// <param name="inventoryItemId">The database record ID of the persisted <c>InventoryItem</c>.</param>
        /// <returns>The storage slot number assigned to the item.</returns>
        public int TryAddItem(Item item, int inventoryItemId)
        {
            var usedSlots = new HashSet<int>(InventorySlots.Select(s => s.SlotNumber));
            var freeSlot = 0;
            while (usedSlots.Contains(freeSlot)) freeSlot++;

            InventorySlots.Add(new InventorySlot
            {
                InventoryItemId = inventoryItemId,
                SlotNumber = freeSlot,
                Item = item,
            });

            return freeSlot;
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
