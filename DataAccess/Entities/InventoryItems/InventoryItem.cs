using GameLibrary;
using GameLibrary.Database.Interfaces;
using System.Data;
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
        public int InventorySlotNumber { get; set; }
        public List<InventoryItemMod> ItemMods { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            InventoryItemId = record["InventoryItemId"].AsInt();
            PlayerId = record["PlayerId"].AsInt();
            ItemId = record["ItemId"].AsInt();
            Rating = record["Rating"].AsInt();
            Equipped = record["Equipped"].AsBool();
            InventorySlotNumber = record["InventorySlotNumber"].AsInt();
            ItemMods = JsonSerializer.Deserialize<List<InventoryItemMod>>(record["ItemModJSON"].AsString()) ?? new List<InventoryItemMod>();
        }
    }
}
