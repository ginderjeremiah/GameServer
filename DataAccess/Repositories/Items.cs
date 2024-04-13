using DataAccess.Entities.Items;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class Items : BaseRepository, IItems
    {
        private static List<Item>? _allItems;
        public Items(string connectionString) : base(connectionString) { }

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

            return QueryToList<Item>(commandText);
        }

        public void AddItem(string itemName, string itemDesc, int itemCategoryId, string iconPath)
        {
            var commandText = @"
                INSERT INTO Items
                VALUES
                    (@ItemName, @ItemDesc, @ItemCategoryId, @IconPath)";

            ExecuteNonQuery(commandText,
                new SqlParameter("@ItemName", itemName),
                new SqlParameter("@ItemDesc", itemDesc),
                new SqlParameter("@ItemCategoryId", itemCategoryId),
                new SqlParameter("@IconPath", iconPath)
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

            ExecuteNonQuery(commandText,
                new SqlParameter("@ItemId", itemId),
                new SqlParameter("@ItemName", itemName),
                new SqlParameter("@ItemDesc", itemDesc),
                new SqlParameter("@ItemCategoryId", itemCategoryId),
                new SqlParameter("@IconPath", iconPath)
            );
        }

        public void DeleteItem(int itemId)
        {
            var commandText = @"
                DELETE Items
                WHERE ItemId = @ItemId";

            ExecuteNonQuery(commandText, new SqlParameter("@ItemId", itemId));
        }
    }

    public interface IItems
    {
        public List<Item> AllItems(bool refreshCache = false);
        public void AddItem(string itemName, string itemDesc, int itemCategoryId, string iconPath);
        public void UpdateItem(int itemId, string itemName, string itemDesc, int itemCategoryId, string iconPath);
        public void DeleteItem(int itemId);
    }
}
