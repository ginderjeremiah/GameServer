namespace GameServer.Models.Items
{
    public class ItemSlot : IModel
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public int SlotTypeId { get; set; }
        public int GuaranteedItemModId { get; set; }
        public decimal Probability { get; set; }

        public ItemSlot() { }

        public ItemSlot(GameCore.Entities.ItemSlot itemSlot)
        {
            Id = itemSlot.Id;
            ItemId = itemSlot.ItemId;
            SlotTypeId = itemSlot.SlotTypeId;
            GuaranteedItemModId = itemSlot.GuaranteedItemModId;
            Probability = itemSlot.Probability;
        }
    }
}
