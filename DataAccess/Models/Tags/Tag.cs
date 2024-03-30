using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Models.Tags
{
    public class Tag : IModel
    {
        public int TagId { get; set; }
        public string TagName { get; set; }
        public string TagCategory { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            TagId = reader["TagId"].AsInt();
            TagName = reader["TagName"].AsString();
            TagCategory = reader["TagCategory"].AsString();
        }
    }
}
