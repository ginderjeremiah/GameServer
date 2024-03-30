using System.Data.SqlClient;

namespace DataAccess.Models
{
    internal interface IModel
    {
        public void LoadFromReader(SqlDataReader reader);
    }
}
