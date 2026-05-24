namespace Game.Api.Models.InventoryItems
{
    public class EquipRequest : IModel
    {
        public int ItemId { get; set; }
        public int EquipmentSlotId { get; set; }
    }
}
