using System.Data;

namespace GameCore.Entities.InventoryItems
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
