namespace Game.Abstractions.Entities
{
    public partial class ItemAttribute
    {
        public int ItemId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }

        public virtual Item Item { get => field ?? throw new NavigationNotLoadedException(nameof(Item)); set; }
        public virtual Attribute Attribute { get => field ?? throw new NavigationNotLoadedException(nameof(Attribute)); set; }
    }
}
