namespace GameServer.Models
{
    public class ItemSlot
    {
        public int ItemSlotId { get; set; }
        public int ItemId { get; set; }
        public int SlotTypeId { get; set; }
        public int GuaranteedId { get; set; }
        public decimal Probability { get; set; }
    }
}
