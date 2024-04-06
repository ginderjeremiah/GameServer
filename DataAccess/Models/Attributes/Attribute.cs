using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Models.Attributes
{
    public class Attribute : IDataModel
    {
        public int AttributeId { get; set; }
        public string AttributeName { get; set; }
        public string AttributeDesc { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            AttributeId = reader["AttributeId"].AsInt();
            AttributeName = reader["AttributeName"].AsString();
            AttributeDesc = reader["AttributeDesc"].AsString();
        }
    }
}
