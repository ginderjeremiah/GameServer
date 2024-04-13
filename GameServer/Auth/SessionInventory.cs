using GameServer.Models.InventoryItems;

namespace GameServer.Auth
{
    public class SessionInventory
    {
        private const int INV_SLOTS = 23;
        private const int EQUIP_SLOTS = 6;
        private readonly List<DataAccess.Entities.InventoryItems.InventoryItem> _sessionInventory;
        public List<InventoryItem?> Inventory { get; set; }
        public List<InventoryItem?> Equipped { get; set; }

        public SessionInventory(List<DataAccess.Entities.InventoryItems.InventoryItem> inventoryItems)
        {
            _sessionInventory = inventoryItems;
            Initialize(inventoryItems);
        }

        private void Initialize(List<DataAccess.Entities.InventoryItems.InventoryItem> inventoryItems)
        {
            var inventory = NewInventoryList();
            var equipped = NewEquippedList();

            foreach (var item in inventoryItems)
            {
                if (item.Equipped)
                {
                    equipped[item.InventorySlotNumber] = new InventoryItem(item);
                }
                else
                {
                    inventory[item.InventorySlotNumber] = new InventoryItem(item);
                }
            }

            Inventory = inventory;
            Equipped = equipped;
        }

        public List<int> GetFreeSlotNumbers()
        {
            return Inventory.Select((item, index) => (item, index)).Where(p => p.item is null).Select(p => p.index).ToList();
        }

        public bool TrySetNewInventoryList(IEnumerable<InventoryUpdate> inventoryUpdates)
        {
            var usedSlots = new HashSet<(bool, int)>();
            var matchedUpdates = _sessionInventory.Select((inv) => (inv, inventoryUpdates.FirstOrDefault(upd => inv.InventoryItemId == upd.InventoryItemId))).ToList();
            var validUpdate = true;

            foreach (var match in matchedUpdates)
            {
                var update = match.Item2;
                if (update != null)
                {
                    var slot = (update.Equipped, update.InventorySlotNumber);
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
            }

            if (validUpdate)
            {
                foreach (var match in matchedUpdates)
                {
                    if (match.Item2 is not null)
                    {
                        match.inv.InventorySlotNumber = match.Item2.InventorySlotNumber;
                        match.inv.Equipped = match.Item2.Equipped;
                    }
                    else
                    {
                        _sessionInventory.Remove(match.inv);
                    }
                }
                Initialize(_sessionInventory);
            }

            return validUpdate;
        }

        private bool IsValidInventoryUpdate(InventoryUpdate item)
        {
            return _sessionInventory.Any(inv => inv.InventoryItemId == item.InventoryItemId)
                && item.InventorySlotNumber is >= 0
                && ((item.Equipped && item.InventorySlotNumber is < EQUIP_SLOTS)
                    || (!item.Equipped && item.InventorySlotNumber is < INV_SLOTS));
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
