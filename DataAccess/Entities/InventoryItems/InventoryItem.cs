using GameLibrary;
using System.Data.SqlClient;
using System.Text.Json;

namespace DataAccess.Entities.InventoryItems
{
    public class InventoryItem : IEntity
    {
        public int InventoryItemId { get; set; }
        public int PlayerId { get; set; }
        public int ItemId { get; set; }
        public int Rating { get; set; }
        public bool Equipped { get; set; }
        public int SlotId { get; set; }
        public List<InventoryItemMod> ItemMods { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            InventoryItemId = reader["InventoryItemId"].AsInt();
            PlayerId = reader["PlayerId"].AsInt();
            ItemId = reader["ItemId"].AsInt();
            Rating = reader["Rating"].AsInt();
            Equipped = reader["Equipped"].AsBool();
            SlotId = reader["SlotId"].AsInt();
            ItemMods = JsonSerializer.Deserialize<List<InventoryItemMod>>(reader["ItemModJSON"].AsString());
        }
    }
}
