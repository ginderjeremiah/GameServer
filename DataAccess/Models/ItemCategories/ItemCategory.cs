using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Models.ItemCategories
{
    public class ItemCategory : IDataModel
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
