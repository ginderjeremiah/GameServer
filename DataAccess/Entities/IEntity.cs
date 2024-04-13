using System.Data.SqlClient;

namespace DataAccess.Entities
{
    internal interface IEntity
    {
        public void LoadFromReader(SqlDataReader reader);
    }
}
