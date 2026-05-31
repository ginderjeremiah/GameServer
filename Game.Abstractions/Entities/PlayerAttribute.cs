namespace Game.Abstractions.Entities
{
    public class PlayerAttribute
    {
        public int PlayerId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }

        public virtual Player Player { get => field ?? throw new NotLoadedException(nameof(Player)); set; }
        public virtual Attribute Attribute { get => field ?? throw new NotLoadedException(nameof(Attribute)); set; }
    }
}
