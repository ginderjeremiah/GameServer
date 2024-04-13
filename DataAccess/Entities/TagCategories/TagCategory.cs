using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.TagCategories
{
    public class TagCategory : IEntity
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
