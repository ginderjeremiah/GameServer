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

        /// <summary>
        /// Slots containing non-equipped items.
        /// </summary>
        public List<InventoryData> InventorySlots { get; set; }

        /// <summary>
        /// Slots containing the currently equipped items.
        /// </summary>
        public List<EquipmentSlot> EquipmentSlots { get; set; }

        /// <summary>
        /// Creates a new <see cref="Inventory"/> instance.
        /// </summary>
        public Inventory()
        {
            EquipmentSlots = NewEquippedList();
        }

        /// <summary>
        /// Gets an <see cref="AttributeModifier"/> collection based on the currently equipped items.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
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
        /// Gets the slot numbers of each free slot in the inventory.
        /// </summary>
        /// <returns></returns>
        public List<int> GetFreeSlotNumbers()
        {
            return InventorySlots.Where(slot => slot.Item is null).Select(slot => slot.SlotNumber).ToList();
        }

        //public bool TrySetNewInventoryList(IEnumerable<IInventoryUpdate> inventoryUpdates)
        //{
        //    var usedSlots = new HashSet<(bool, int)>();
        //    var matchedUpdates = _sessionInventory.Select((inv) => (inv, inventoryUpdates.FirstOrDefault(upd => inv.Id == upd.Id))).ToList();
        //    var validUpdate = true;

        //    foreach (var match in matchedUpdates)
        //    {
        //        var update = match.Item2;
        //        if (update != null)
        //        {
        //            var slot = (update.Equipped, update.InventorySlotNumber);
        //            if (usedSlots.Contains(slot) || !IsValidInventoryUpdate(update))
        //            {
        //                validUpdate = false;
        //                break;
        //            }
        //            else
        //            {
        //                usedSlots.Add(slot);
        //            }
        //        }
        //    }

        //    if (validUpdate)
        //    {
        //        foreach (var match in matchedUpdates)
        //        {
        //            if (match.Item2 is not null)
        //            {
        //                match.inv.InventorySlotNumber = match.Item2.InventorySlotNumber;
        //                match.inv.Equipped = match.Item2.Equipped;
        //            }
        //            else
        //            {
        //                _sessionInventory.Remove(match.inv);
        //            }
        //        }
        //    }

        //    return validUpdate;
        //}

        //private bool IsValidInventoryUpdate(IInventoryUpdate item)
        //{
        //    return _sessionInventory.Any(inv => inv.Id == item.Id)
        //        && item.InventorySlotNumber is >= 0
        //        && ((item.Equipped && item.InventorySlotNumber is < EQUIP_SLOTS)
        //            || (!item.Equipped && item.InventorySlotNumber is < INV_SLOTS));
        //}

        private static List<EquipmentSlot> NewEquippedList()
        {
            return Enumerable.Range(1, EquipSlots)
                .Select(index => new EquipmentSlot((EEquipmentSlot)index)
                {
                    Item = null,
                }).ToList();
        }
    }
}
