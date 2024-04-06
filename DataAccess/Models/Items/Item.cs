using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Models.Items
{
    public class Item : IDataModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemDesc { get; set; }
        public int ItemCategoryId { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            ItemId = reader["ItemId"].AsInt();
            ItemName = reader["ItemName"].AsString();
            ItemDesc = reader["ItemDesc"].AsString();
            ItemCategoryId = reader["ItemCategoryId"].AsInt();
        }
    }
}
