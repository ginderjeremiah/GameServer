namespace GameCore.Entities
{
    public class Item
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int ItemCategoryId { get; set; }
        public string IconPath { get; set; }

        public virtual List<ItemAttribute> ItemAttributes { get; set; }
        public virtual ItemCategory ItemCategory { get; set; }
        public virtual List<ItemSlot> ItemSlots { get; set; }
        public virtual List<Tag> Tags { get; set; }
        public virtual List<EnemyDrop> EnemyDrops { get; set; }
        public virtual List<ZoneDrop> ZoneDrops { get; set; }
        public virtual List<InventoryItem> InventoryItems { get; set; }
    }
}
