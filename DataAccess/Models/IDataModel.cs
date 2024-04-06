using System.Data.SqlClient;

namespace DataAccess.Models
{
    internal interface IDataModel
    {
        public void LoadFromReader(SqlDataReader reader);
    }
}
