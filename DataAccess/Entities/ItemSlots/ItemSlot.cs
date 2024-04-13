using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.ItemSlots
{
    public class ItemSlot : IEntity
    {
        public int ItemSlotId { get; set; }
        public int ItemId { get; set; }
        public int SlotTypeId { get; set; }
        public int GuaranteedId { get; set; }
        public decimal Probability { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            ItemSlotId = reader["ItemSlotId"].AsInt();
            ItemId = reader["ItemId"].AsInt();
            SlotTypeId = reader["SlotTypeId"].AsInt();
            GuaranteedId = reader["GuaranteedId"].AsInt();
            Probability = reader["Probability"].AsDecimal();
        }
    }
}
