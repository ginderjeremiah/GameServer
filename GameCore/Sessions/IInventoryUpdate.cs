namespace GameCore.Sessions
{
    public interface IInventoryUpdate
    {
        public int InventoryItemId { get; set; }
        public int InventorySlotNumber { get; set; }
        public bool Equipped { get; set; }
    }
}
