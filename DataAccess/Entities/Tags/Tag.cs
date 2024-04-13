using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.Tags
{
    public class Tag : IEntity
    {
        public int TagId { get; set; }
        public string TagName { get; set; }
        public int TagCategoryId { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            TagId = reader["TagId"].AsInt();
            TagName = reader["TagName"].AsString();
            TagCategoryId = reader["TagCategoryId"].AsInt();
        }
    }
}
