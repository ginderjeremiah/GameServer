namespace Game.Core.Rarity
{
    /// <inheritdoc cref="ERarity"/>
    public class Rarity
    {
        public ERarity Id { get; }

        public string Name { get; }

        public Rarity(ERarity id)
        {
            Id = id;
            Name = id.ToString().SpaceWords();
        }
    }
}
