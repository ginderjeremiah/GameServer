namespace Game.Api.Models.InventoryItems
{
    public class InventoryData : IModel
    {
        public List<InventoryItem?> Inventory { get; set; } = [];
        public List<InventoryItem?> Equipped { get; set; } = [];
    }
}
