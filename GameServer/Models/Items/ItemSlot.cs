namespace GameServer.Models.Items
{
    public class ItemSlot : IModel
    {
        public int ItemSlotId { get; set; }
        public int ItemId { get; set; }
        public int SlotTypeId { get; set; }
        public int GuaranteedId { get; set; }
        public decimal Probability { get; set; }

        public ItemSlot() { }

        public ItemSlot(GameCore.Entities.ItemSlots.ItemSlot itemSlot)
        {
            ItemSlotId = itemSlot.ItemId;
            ItemId = itemSlot.ItemId;
            SlotTypeId = itemSlot.SlotTypeId;
            GuaranteedId = itemSlot.GuaranteedId;
            Probability = itemSlot.Probability;
        }
    }
}
