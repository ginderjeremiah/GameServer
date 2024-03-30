namespace GameServer.Models.Request
{
    public class InventoryUpdate
    {
        public int InventoryItemId { get; set; }
        public int SlotId { get; set; }
        public bool Equipped { get; set; }
    }
}
