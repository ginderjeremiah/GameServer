namespace Game.Abstractions.Entities
{
    public class Role
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public virtual List<User> Users { get => field ?? throw new NotLoadedException(nameof(Users)); set; }
    }
}
