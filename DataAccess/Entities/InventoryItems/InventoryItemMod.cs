using GameCore;
using GameCore.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.InventoryItems
{
    public class InventoryItemMod : IEntity
    {
        public int ItemModId { get; set; }
        public int ItemSlotId { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            ItemModId = record["ItemModId"].AsInt();
            ItemSlotId = record["ItemSlotId"].AsInt();
        }
    }
}
