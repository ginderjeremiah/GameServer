using DataAccess.Models.Items;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class Items : BaseRepository, IItems
    {
        public Items(string connectionString) : base(connectionString) { }

        public List<Item> AllItems()
        {
            var commandText = @"
                SELECT
                    ItemId,
                    ItemName,
                    ItemDesc,
                    ItemCategoryId
                FROM
                    Items";

            return QueryToList<Item>(commandText);
        }

        public void AddItem(string itemName, string itemDesc, int itemCategoryId)
        {
            var commandText = @"
                INSERT INTO Items
                VALUES
                    (@ItemName, @ItemDesc, @ItemCategoryId)";

            ExecuteNonQuery(commandText,
                            new SqlParameter("@ItemName", itemName),
                            new SqlParameter("@ItemDesc", itemDesc),
                            new SqlParameter("@ItemCategoryId", itemCategoryId)
                        );
        }

        public void UpdateItem(int itemId, string itemName, string itemDesc, int itemCategoryId)
        {
            var commandText = @"
                UPDATE Items
                SET ItemName = @ItemName,
                    ItemDesc = @ItemDesc,
                    ItemCategoryId = @ItemCategoryId
                WHERE ItemId = @ItemId";

            ExecuteNonQuery(commandText,
                            new SqlParameter("@ItemId", itemId),
                            new SqlParameter("@ItemName", itemName),
                            new SqlParameter("@ItemDesc", itemDesc),
                            new SqlParameter("@ItemCategoryId", itemCategoryId)
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
        public List<Item> AllItems();
        public void AddItem(string itemName, string itemDesc, int itemCategoryId);
        public void UpdateItem(int itemId, string itemName, string itemDesc, int itemCategoryId);
        public void DeleteItem(int itemId);
    }
}
