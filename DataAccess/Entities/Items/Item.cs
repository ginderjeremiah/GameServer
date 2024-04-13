using GameLibrary;
using System.Data.SqlClient;
using System.Text.Json;

namespace DataAccess.Entities.Items
{
    public class Item : IEntity
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string ItemDesc { get; set; }
        public int ItemCategoryId { get; set; }
        public List<ItemAttribute> Attributes { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            ItemId = reader["ItemId"].AsInt();
            ItemName = reader["ItemName"].AsString();
            ItemDesc = reader["ItemDesc"].AsString();
            ItemCategoryId = reader["ItemCategoryId"].AsInt();
            Attributes = JsonSerializer.Deserialize<List<ItemAttribute>>(reader["AttributesJSON"].AsString());
        }
    }
}
