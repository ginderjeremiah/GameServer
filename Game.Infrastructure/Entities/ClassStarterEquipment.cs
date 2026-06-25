namespace Game.Infrastructure.Entities
{
    /// <summary>An item a <see cref="Class"/> starts with equipped, keyed by its equipment slot (one item per
    /// slot). The item's innate skill (<see cref="Item.GrantedSkillId"/>) comes online at creation through the
    /// normal equip path.</summary>
    public class ClassStarterEquipment
    {
        public int ClassId { get; set; }

        /// <summary>The equipment slot the item is equipped into (an <see cref="Core.EEquipmentSlot"/>).</summary>
        public int EquipmentSlotId { get; set; }

        public int ItemId { get; set; }

        public virtual Class Class { get => field ?? throw new NotLoadedException(nameof(Class)); set; }
        public virtual Item Item { get => field ?? throw new NotLoadedException(nameof(Item)); set; }
    }
}
