using GameCore;
using GameCore.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.ItemSlots
{
    public class ItemSlot : IEntity
    {
        public int ItemSlotId { get; set; }
        public int ItemId { get; set; }
        public int SlotTypeId { get; set; }
        public int GuaranteedId { get; set; }
        public decimal Probability { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            ItemSlotId = record["ItemSlotId"].AsInt();
            ItemId = record["ItemId"].AsInt();
            SlotTypeId = record["SlotTypeId"].AsInt();
            GuaranteedId = record["GuaranteedId"].AsInt();
            Probability = record["Probability"].AsDecimal();
        }
    }
}
