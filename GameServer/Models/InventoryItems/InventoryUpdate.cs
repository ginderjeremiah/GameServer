namespace GameServer.Models.InventoryItems
{
    public class InventoryUpdate : IModel
    {
        public int InventoryItemId { get; set; }
        public int SlotId { get; set; }
        public bool Equipped { get; set; }
    }
}
