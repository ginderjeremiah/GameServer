namespace Game.Core.Entities
{
    public partial class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int TagCategoryId { get; set; }

        public virtual TagCategory TagCategory { get; set; }
        public virtual List<Item> Items { get; set; }
        public virtual List<ItemMod> ItemMods { get; set; }
    }
}
