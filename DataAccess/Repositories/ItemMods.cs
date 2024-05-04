using DataAccess.Entities.ItemMods;
using GameCore.Database;
using GameCore.Database.Interfaces;

namespace DataAccess.Repositories
{
    internal class ItemMods : BaseRepository, IItemMods
    {
        private static List<ItemMod>? _allMods;
        private static readonly List<Dictionary<int, List<ItemModWithoutAttributes>>?> _itemModsBySlot = new();
        private static readonly object _lockForItem = new();

        public ItemMods(IDataProvider database) : base(database) { }

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
                    IM.ItemModId,
                    IM.ItemModName,
                    IM.Removable,
                    IM.ItemModDesc,
                    IM.SlotTypeId,
	                COALESCE(AttJSON.JSONData, '[]') AS AttributesJSON
                FROM ItemMods IM
                OUTER APPLY (
	                SELECT
                        IMA.ItemModId,
		                IMA.AttributeId,
		                IMA.Amount
	                FROM ItemModAttributes IMA
	                WHERE IMA.ItemModId = IM.ItemModId
	                FOR JSON PATH
                ) AS AttJSON(JSONData)
                ORDER BY ItemModId";

            return Database.QueryToList<ItemMod>(commandText);
        }

        public void AddItemMod(string itemModName, bool removable, string itemModDesc)
        {
            var commandText = @"
                INSERT INTO ItemMods
                VALUES
                    (@ItemModName, @Removable, @ItemModDesc)";

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@ItemModName", itemModName),
                new QueryParameter("@Removable", removable),
                new QueryParameter("@ItemModDesc", itemModDesc)
            );
        }
        public void UpdateItemMod(int itemModId, string itemModName, bool removable, string itemModDesc)
        {
            var commandText = @"
                UPDATE ItemMods
                SET ItemModName = @ItemModName,
                    Removable = @Removable,
                    ItemModDesc = @ItemModDesc
                WHERE ItemModId = @ItemModId";

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@ItemModName", itemModName),
                new QueryParameter("@Removable", removable),
                new QueryParameter("@ItemModDesc", itemModDesc),
                new QueryParameter("@ItemModId", itemModId)
            );
        }
        public void DeleteItemMod(int itemModId)
        {
            var commandText = @"
                DELETE ItemMods
                WHERE ItemModId = @ItemModId";

            Database.ExecuteNonQuery(commandText, new QueryParameter("@ItemModId", itemModId));
        }

        public Dictionary<int, List<ItemModWithoutAttributes>> GetModsForItemBySlot(int itemId)
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

        private Dictionary<int, List<ItemModWithoutAttributes>> ModsForItemBySlot(int itemId)
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

            return Database.QueryToList<ItemModWithoutAttributes>(commandText, new QueryParameter("@ItemId", itemId))
                .GroupBy(mod => mod.SlotTypeId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }
    }

    public interface IItemMods
    {
        public List<ItemMod> AllItemMods(bool refreshCache = false);
        public Dictionary<int, List<ItemModWithoutAttributes>> GetModsForItemBySlot(int itemId);
        public void AddItemMod(string itemModName, bool removable, string itemModDesc);
        public void UpdateItemMod(int itemModId, string itemModName, bool removable, string itemModDesc);
        public void DeleteItemMod(int itemModId);
    }
}
