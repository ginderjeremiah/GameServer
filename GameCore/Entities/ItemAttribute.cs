namespace GameCore.Entities
{
    public class ItemAttribute
    {
        public int ItemId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }

        public virtual Item Item { get; set; }
        public virtual Attribute Attribute { get; set; }
    }
}
