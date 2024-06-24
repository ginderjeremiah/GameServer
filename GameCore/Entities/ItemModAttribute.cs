namespace GameCore.Entities
{
    public class ItemModAttribute
    {
        public int ItemModId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }

        public virtual ItemMod ItemMod { get; set; }
        public virtual Attribute Attribute { get; set; }
    }
}
