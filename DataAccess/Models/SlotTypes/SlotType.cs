using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Models.SlotTypes
{
    public class SlotType : IModel
    {
        public int SlotTypeId { get; set; }
        public string SlotTypeName { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            SlotTypeId = reader["SlotTypeId"].AsInt();
            SlotTypeName = reader["SlotTypeName"].AsString();
        }
    }
}
