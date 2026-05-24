namespace Game.Api.Models.InventoryItems
{
    public class InventoryData : IModel
    {
        public List<InventoryItem> UnlockedItems { get; set; } = [];
        public List<int> UnlockedMods { get; set; } = [];
    }
}
