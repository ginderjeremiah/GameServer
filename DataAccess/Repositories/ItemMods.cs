using DataAccess.Models.ItemMods;
using GameServer.Models;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class ItemMods : BaseRepository, IItemMods
    {
        private static List<ItemMod>? _allMods;
        private static readonly List<Dictionary<int, List<ItemMod>>?> _itemModsBySlot = new();
        private static readonly object _lockForItem = new();

        public ItemMods(string connectionString) : base(connectionString) { }

        public List<ItemMod> AllItemMods(bool refreshCache = false)
        {
            if (_allMods is null || refreshCache)
            {
                _allMods = GetItemMods();
            }
            return _allMods;
        }

        private List<ItemMod> GetItemMods()
        {
            var commandText = @"
                SELECT
                    ItemModId,
                    ItemModName,
                    Removable,
                    ItemModDesc,
                    SlotTypeId
                FROM ItemMods
                ORDER BY ItemModId";

            return QueryToList<ItemMod>(commandText);
        }

        public void AddItemMod(string itemModName, bool removable, string itemModDesc)
        {
            var commandText = @"
                INSERT INTO ItemMods
                VALUES
                    (@ItemModName, @Removable, @ItemModDesc)";

            ExecuteNonQuery(commandText, new SqlParameter("@ItemModName", itemModName), new SqlParameter("@Removable", removable), new SqlParameter("@ItemModDesc", itemModDesc));
        }
        public void UpdateItemMod(int itemModId, string itemModName, bool removable, string itemModDesc)
        {
            var commandText = @"
                UPDATE ItemMods
                SET ItemModName = @ItemModName,
                    Removable = @Removable,
                    ItemModDesc = @ItemModDesc
                WHERE ItemModId = @ItemModId";

            ExecuteNonQuery(commandText,
                            new SqlParameter("@ItemModName", itemModName),
                            new SqlParameter("@Removable", removable),
                            new SqlParameter("@ItemModDesc", itemModDesc),
                            new SqlParameter("@ItemModId", itemModId)
                        );
        }
        public void DeleteItemMod(int itemModId)
        {
            var commandText = @"
                DELETE ItemMods
                WHERE ItemModId = @ItemModId";

            ExecuteNonQuery(commandText, new SqlParameter("@ItemModId", itemModId));
        }

        public ItemMod? GetItemMod(ItemSlot itemSlot, Random rng, List<ItemMod> exclList)
        {
            if (itemSlot.GuaranteedId == -1)
            {
                var mods = GetModsForItemBySlot(itemSlot.ItemId);
                if (mods.TryGetValue(itemSlot.SlotTypeId, out var itemMods))
                {
                    //TODO Add weights for item mods
                    var actualMods = itemMods.Except(exclList).ToList();
                    return actualMods.Any()
                        ? actualMods[rng.Next(0, actualMods.Count - 1)]
                        : null;
                }
            }
            else
            {
                return AllItemMods()[itemSlot.GuaranteedId];
            }

            return null;
        }

        public Dictionary<int, List<ItemMod>> GetModsForItemBySlot(int itemId)
        {
            if (itemId >= _itemModsBySlot.Count || _itemModsBySlot[itemId] is null)
            {
                lock (_lockForItem)
                {
                    for (int i = _itemModsBySlot.Count; i <= itemId + 1; i++)
                    {
                        _itemModsBySlot.Add(null);
                    }
                    _itemModsBySlot[itemId] ??= ModsForItemBySlot(itemId);
                }
            }
            return _itemModsBySlot[itemId];
        }

        private Dictionary<int, List<ItemMod>> ModsForItemBySlot(int itemId)
        {
            var commandText = @"
                SELECT DISTINCT
                    IM.ItemModId,
                    IM.ItemModName,
                    IM.Removable,
                    IM.ItemModDesc,
                    IM.SlotTypeId
                FROM ItemMods AS IM
                INNER JOIN ItemModTags AS IMT
                ON IM.ItemModId = IMT.ItemModId
                INNER JOIN ItemTags AS IT
                ON IT.TagId = IMT.TagId
                INNER JOIN Items as I
                ON I.ItemId = IT.ItemId
                WHERE I.ItemId = @ItemId";

            return QueryToList<ItemMod>(commandText, new SqlParameter("@ItemId", itemId))
                    .GroupBy(mod => mod.SlotTypeId)
                    .ToDictionary(g => g.Key, g => g.ToList());
        }
    }

    public interface IItemMods
    {
        public List<ItemMod> AllItemMods(bool refreshCache = false);
        public ItemMod? GetItemMod(ItemSlot itemSlot, Random rng, List<ItemMod> exclList);
        public Dictionary<int, List<ItemMod>> GetModsForItemBySlot(int itemId);
        public void AddItemMod(string itemModName, bool removable, string itemModDesc);
        public void UpdateItemMod(int itemModId, string itemModName, bool removable, string itemModDesc);
        public void DeleteItemMod(int itemModId);
    }
}
