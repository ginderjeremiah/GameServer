using DataAccess.Models.InventoryItems;
using GameServer.Models.Request;

namespace GameServer.Auth
{
    public class SessionInventory
    {
        private const int INV_SLOTS = 23;
        private const int EQUIP_SLOTS = 6;
        private readonly List<InventoryItem> _sessionInventory;
        public List<InventoryItem?> Inventory { get; set; }
        public List<InventoryItem?> Equipped { get; set; }

        public SessionInventory(List<InventoryItem> inventoryItems)
        {
            _sessionInventory = inventoryItems;
            Initialize(inventoryItems);
        }

        private void Initialize(List<InventoryItem> inventoryItems)
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

        public List<int> GetFreeSlotIds()
        {
            return Inventory.Select((item, index) => (item, index)).Where(p => p.item is null).Select(p => p.index).ToList();
        }

        public bool TrySetNewInventoryList(IEnumerable<InventoryUpdate> inventoryUpdates)
        {
            var usedSlots = new HashSet<(bool, int)>();
            var matchedUpdates = _sessionInventory.Select((inv, i) => (inv, inventoryUpdates.FirstOrDefault(upd => inv.InventoryItemId == upd.InventoryItemId), i)).ToList();
            var validUpdate = true;

            foreach (var match in matchedUpdates.Where(match => match.Item2 is not null))
            {
                var update = match.Item2;
                var slot = (update.Equipped, update.SlotId);
                if (usedSlots.Contains(slot) || !IsValidInventoryUpdate(update))
                {
                    validUpdate = false;
                    break;
                }
                else
                {
                    usedSlots.Add(slot);
                }
            }

            if (validUpdate)
            {
                foreach (var match in matchedUpdates)
                {
                    if (match.Item2 is not null)
                    {
                        match.inv.SlotId = match.Item2.SlotId;
                        match.inv.Equipped = match.Item2.Equipped;
                    }
                    else
                    {
                        _sessionInventory.RemoveAt(match.i);
                    }
                }
                Initialize(_sessionInventory);
            }

            return validUpdate;
        }

        private bool IsValidInventoryUpdate(InventoryUpdate item)
        {
            return _sessionInventory.Any(inv => inv.InventoryItemId == item.InventoryItemId)
                && item.SlotId is >= 0
                && ((item.Equipped && item.SlotId is < EQUIP_SLOTS)
                    || (!item.Equipped && item.SlotId is < INV_SLOTS));
        }

        private static List<InventoryItem?> NewEquippedList()
        {
            return Enumerable.Repeat<InventoryItem?>(null, EQUIP_SLOTS).ToList();
        }

        private static List<InventoryItem?> NewInventoryList()
        {
            return Enumerable.Repeat<InventoryItem?>(null, INV_SLOTS).ToList();
        }
    }
}
