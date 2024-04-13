using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.SlotTypes
{
    public class SlotType : IEntity
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
