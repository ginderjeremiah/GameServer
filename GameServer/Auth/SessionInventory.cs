using DataAccess.Models.InventoryItems;
using GameServer.Models.Request;

namespace GameServer.Auth
{
    public class SessionInventory
    {
        private const int INV_SLOTS = 23;
        private const int EQUIP_SLOTS = 6;
        public List<InventoryItem?> Inventory { get; set; }
        public List<InventoryItem?> Equipped { get; set; }

        public SessionInventory(IEnumerable<InventoryItem> inventoryItems)
        {
            var inventory = NewInventoryList();
            var equipped = NewEquippedList();

            foreach (var item in inventoryItems)
            {
                if (item.Equipped)
                {
                    equipped[item.SlotId] = item;
                }
                else
                {
                    inventory[item.SlotId] = item;
                }
            }

            Inventory = inventory;
            Equipped = equipped;
        }

        //public int GetNumFreeSlots()
        //{
        //    return Inventory.Where(item => item is null).Count();
        //}

        //public int GetNextFreeSlotId()
        //{
        //    for (var i = 0; i < Inventory.Count; i++)
        //    {
        //        if (Inventory[i] is null)
        //            return i;
        //    }
        //    return -1;
        //}

        public List<int> GetFreeSlotIds()
        {
            return Inventory.Select((item, index) => (item, index)).Where(p => p.item is null).Select(p => p.index).ToList();
        }

        public bool TrySetNewEquippedList(IEnumerable<(InventoryUpdate, InventoryItem)> updateItems)
        {
            if (updateItems.Any(upd => upd.Item1.SlotId is < 0 or >= EQUIP_SLOTS))
            {
                return false;
            }

            foreach (var item in updateItems)
            {
                var targetSlot = item.Item1.SlotId;
                var target = Equipped[targetSlot];

                if (item.Item2.Equipped)
                    Equipped[item.Item2.SlotId] = target;
                else
                    Inventory[item.Item2.SlotId] = target;

                if (target is not null)
                {
                    target.SlotId = item.Item2.SlotId;
                    target.Equipped = item.Item2.Equipped;
                }

                Equipped[targetSlot] = item.Item2;
                item.Item2.SlotId = targetSlot;
                item.Item2.Equipped = true;
            }

            return true;
        }

        public bool TrySetNewInventoryList(IEnumerable<(InventoryUpdate, InventoryItem)> updateItems)
        {
            if (updateItems.Any(upd => upd.Item1.SlotId is < 0 or >= INV_SLOTS || upd.Item2.Equipped))
            {
                return false;
            }

            foreach (var item in updateItems)
            {
                var targetSlot = item.Item1.SlotId;
                var target = Inventory[targetSlot];

                Inventory[item.Item2.SlotId] = target;

                if (target is not null)
                {
                    target.SlotId = item.Item2.SlotId;
                }

                Inventory[targetSlot] = item.Item2;
                item.Item2.SlotId = targetSlot;
            }

            return true;
        }

        private List<InventoryItem?> NewEquippedList()
        {
            return Enumerable.Repeat<InventoryItem?>(null, EQUIP_SLOTS).ToList();
        }

        private List<InventoryItem?> NewInventoryList()
        {
            return Enumerable.Repeat<InventoryItem?>(null, INV_SLOTS).ToList();
        }
    }
}
