using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.InventoryItems
{
    public class InventoryItemMod : IEntity
    {
        public int ItemModId { get; set; }
        public int ItemSlotId { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            ItemModId = reader["ItemModId"].AsInt();
            ItemSlotId = reader["ItemSlotId"].AsInt();
        }
    }
}
