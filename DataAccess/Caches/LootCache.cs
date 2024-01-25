using DataAccess.Models.InventoryItems;
using DataAccess.Models.ItemMods;
using GameServer.Models;
using System.Data;

namespace DataAccess.Caches
{
    internal class LootCache : ILootCache
    {
        private readonly IRepositoryManager _repositoryManager;
        private readonly IEnemyCache _enemyCache;
        private readonly IZoneCache _zoneCache;
        private readonly List<Dictionary<int, List<ItemMod>>?> _itemModsBySlot = new();
        private readonly List<ItemMod> _allMods;
        private readonly List<List<ItemSlot>?> _itemSlots = new();
        private readonly object _lockForItem = new();
        private readonly object _lockForItemSlot = new();

        public LootCache(IRepositoryManager repositoryManager, IEnemyCache enemyCache, IZoneCache zoneCache)
        {
            _repositoryManager = repositoryManager;
            _allMods = repositoryManager.ItemMods.AllItemMods();
            _enemyCache = enemyCache;
            _zoneCache = zoneCache;
        }

        public List<ItemMod> AllMods()
        {
            return _allMods;
        }

        public List<InventoryItem> RollDrops(int enemyId, int zoneId, int max)
        {
            var rng = new Random();
            var drops = new List<InventoryItem>();
            foreach (var drop in _enemyCache.GetEnemy(enemyId).EnemyDrops.Where(d => (decimal)rng.NextSingle() < d.DropRate))
            {
                if (drops.Count < max)
                {
                    drops.Add(GetItemInstance(drop.ItemId, rng));
                }
            }
            foreach (var drop in _zoneCache.GetZone(zoneId).ZoneDrops.Where(d => (decimal)rng.NextSingle() < d.DropRate))
            {
                if (drops.Count < max)
                {
                    drops.Add(GetItemInstance(drop.ItemId, rng));
                }
            }
            return drops;
        }

        private InventoryItem GetItemInstance(int itemId, Random rng)
        {
            var slots = GetSlotsForItem(itemId);
            var itemMods = new List<ItemMod>();
            var inventoryItemMods = new List<InventoryItemMod>();

            foreach (var slot in slots.Where(s => (decimal)rng.NextSingle() < s.Probability))
            {
                var mod = GetItemMod(slot, rng, itemMods);
                if (mod is not null)
                {
                    itemMods.Add(mod);
                    inventoryItemMods.Add(new InventoryItemMod
                    {
                        ItemModId = mod.ItemModId,
                        ItemSlotId = slot.ItemSlotId,
                    });
                }
            }

            return new InventoryItem
            {
                ItemId = itemId,
                Rating = 0, //TODO: implement Rating calculation
                Equipped = false,
                ItemMods = inventoryItemMods
            };
        }

        private ItemMod? GetItemMod(ItemSlot itemSlot, Random rng, List<ItemMod> exclList)
        {
            if (itemSlot.GuaranteedId == 0)
            {
                var mods = GetModsForItem(itemSlot.ItemId);
                if (mods.TryGetValue(itemSlot.SlotTypeId, out var itemMods))
                {
                    //TODO Add weights for item mods
                    var actualMods = itemMods.Except(exclList).ToList();
                    return actualMods.Any()
                        ? actualMods[rng.Next(0, actualMods.Count() - 1)]
                        : null;
                }
            }
            else
            {
                return _allMods[itemSlot.GuaranteedId];
            }

            return null;
        }

        private Dictionary<int, List<ItemMod>> GetModsForItem(int itemId)
        {
            if (itemId >= _itemModsBySlot.Count || _itemModsBySlot[itemId] is null)
            {
                lock (_lockForItem)
                {
                    for (int i = _itemModsBySlot.Count; i <= itemId + 1; i++)
                    {
                        _itemModsBySlot.Add(null);
                    }
                    _itemModsBySlot[itemId] ??= _repositoryManager.ItemMods.GetModsForItemBySlot(itemId);
                }
            }
            return _itemModsBySlot[itemId];
        }

        private List<ItemSlot> GetSlotsForItem(int itemId)
        {
            if (itemId >= _itemSlots.Count || _itemSlots[itemId] is null)
            {
                lock (_lockForItemSlot)
                {
                    for (int i = _itemSlots.Count; i <= itemId + 1; i++)
                    {
                        _itemSlots.Add(null);
                    }
                    _itemSlots[itemId] ??= _repositoryManager.ItemSlots.SlotsForItem(itemId);
                }
            }
            return _itemSlots[itemId];
        }
    }

    public interface ILootCache
    {
        public List<ItemMod> AllMods();
        public List<InventoryItem> RollDrops(int enemyId, int zoneId, int max);
    }
}
