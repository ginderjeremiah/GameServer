namespace Game.Infrastructure.Entities
{
    public class TagCategory
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public virtual List<Tag> Tags { get => field ?? throw new NotLoadedException(nameof(Tags)); set; }
    }
}
