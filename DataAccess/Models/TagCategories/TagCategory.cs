using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Models.TagCategories
{
    public class TagCategory : IDataModel
    {
        public int TagCategoryId { get; set; }
        public string TagCategoryName { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            TagCategoryId = reader["TagCategoryId"].AsInt();
            TagCategoryName = reader["TagCategoryName"].AsString();
        }
    }
}
