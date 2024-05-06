using GameCore.DataAccess;
using GameCore.Entities.Items;
using GameCore.Infrastructure;

namespace DataAccess.Repositories
{
    internal class Items : BaseRepository, IItems
    {

        private static List<Item>? _allItems;
        public Items(IDatabaseService database) : base(database) { }

        public List<Item> AllItems(bool refreshCache = false)
        {
            if (_allItems is null || refreshCache)
            {
                _allItems = GetItems();
            }
            return _allItems;
        }

        private List<Item> GetItems()
        {
            var commandText = @"
                SELECT
                    I.ItemId,
                    I.ItemName,
                    I.ItemDesc,
                    I.ItemCategoryId,
                    I.IconPath,
	                COALESCE(AttJSON.JSONData, '[]') AS AttributesJSON
                FROM
                    Items I
                OUTER APPLY (
	                SELECT
                        IA.ItemId,
		                IA.AttributeId,
		                IA.Amount
	                FROM ItemAttributes IA
	                WHERE IA.ItemId = I.ItemId
	                FOR JSON PATH
                ) AS AttJSON(JSONData)";

            return Database.QueryToList<Item>(commandText);
        }

        public void AddItem(string itemName, string itemDesc, int itemCategoryId, string iconPath)
        {
            var commandText = @"
                INSERT INTO Items
                VALUES
                    (@ItemName, @ItemDesc, @ItemCategoryId, @IconPath)";

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@ItemName", itemName),
                new QueryParameter("@ItemDesc", itemDesc),
                new QueryParameter("@ItemCategoryId", itemCategoryId),
                new QueryParameter("@IconPath", iconPath)
            );
        }

        public void UpdateItem(int itemId, string itemName, string itemDesc, int itemCategoryId, string iconPath)
        {
            var commandText = @"
                UPDATE Items
                SET ItemName = @ItemName,
                    ItemDesc = @ItemDesc,
                    ItemCategoryId = @ItemCategoryId,
                    IconPath = @IconPath
                WHERE ItemId = @ItemId";

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@ItemId", itemId),
                new QueryParameter("@ItemName", itemName),
                new QueryParameter("@ItemDesc", itemDesc),
                new QueryParameter("@ItemCategoryId", itemCategoryId),
                new QueryParameter("@IconPath", iconPath)
            );
        }

        public void DeleteItem(int itemId)
        {
            var commandText = @"
                DELETE Items
                WHERE ItemId = @ItemId";

            Database.ExecuteNonQuery(commandText, new QueryParameter("@ItemId", itemId));
        }
    }
}
