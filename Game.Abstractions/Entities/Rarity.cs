namespace Game.Abstractions.Entities
{
    public class Rarity
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public virtual List<Item> Items { get => field ?? throw new NotLoadedException(nameof(Items)); set; }
    }
}
