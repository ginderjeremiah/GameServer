namespace Game.Infrastructure.Entities
{
    public class Rarity
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public virtual List<Item> Items { get => field ?? throw new NotLoadedException(nameof(Items)); set; }
        public virtual List<ItemMod> ItemMods { get => field ?? throw new NotLoadedException(nameof(ItemMods)); set; }
    }
}
