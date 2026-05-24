namespace Game.Api.Models.InventoryItems
{
    public class RemoveModRequest : IModel
    {
        public int ItemId { get; set; }
        public int ItemModSlotId { get; set; }
    }
}
