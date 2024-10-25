using GameCore;

namespace GameServer.Models.Items
{
    public class ItemModSlot : IModel
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public EItemModSlotType ItemModSlotTypeId { get; set; }
        public int? GuaranteedItemModId { get; set; }
        public decimal Probability { get; set; }

        public ItemModSlot() { }

        public ItemModSlot(GameCore.Entities.ItemModSlot itemSlot)
        {
            Id = itemSlot.Id;
            ItemId = itemSlot.ItemId;
            ItemModSlotTypeId = (EItemModSlotType)itemSlot.ItemModSlotTypeId;
            GuaranteedItemModId = itemSlot.GuaranteedItemModId;
            Probability = itemSlot.Probability;
        }
    }
}
