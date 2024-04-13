using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.ItemCategories
{
    public class ItemCategory : IEntity
    {
        public int ItemCategoryId { get; set; }
        public string CategoryName { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            ItemCategoryId = reader["ItemCategoryId"].AsInt();
            CategoryName = reader["CategoryName"].AsString();
        }
    }
}
