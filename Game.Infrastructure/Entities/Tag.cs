namespace Game.Infrastructure.Entities
{
    public class Tag
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int TagCategoryId { get; set; }

        public virtual TagCategory TagCategory { get => field ?? throw new NotLoadedException(nameof(TagCategory)); set; }
        public virtual List<Item> Items { get => field ?? throw new NotLoadedException(nameof(Items)); set; }
        public virtual List<ItemMod> ItemMods { get => field ?? throw new NotLoadedException(nameof(ItemMods)); set; }
    }
}
