namespace Game.Abstractions.Entities
{
    public partial class PlayerAttribute
    {
        public int PlayerId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }

        public virtual Player Player { get; set; }
        public virtual Attribute Attribute { get; set; }
    }
}
