namespace Game.Infrastructure.Entities
{
    public class ItemModAttribute
    {
        public int ItemModId { get; set; }
        public int AttributeId { get; set; }
        public decimal Amount { get; set; }

        public virtual ItemMod ItemMod { get => field ?? throw new NotLoadedException(nameof(ItemMod)); set; }
        public virtual Attribute Attribute { get => field ?? throw new NotLoadedException(nameof(Attribute)); set; }
    }
}
