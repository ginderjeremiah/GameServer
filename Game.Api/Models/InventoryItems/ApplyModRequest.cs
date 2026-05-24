namespace Game.Api.Models.InventoryItems
{
    public class ApplyModRequest : IModel
    {
        public int ItemId { get; set; }
        public int ItemModId { get; set; }
        public int ItemModSlotId { get; set; }
    }
}
