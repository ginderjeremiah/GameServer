namespace Game.Abstractions.Entities
{
    public partial class ItemCategory
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public virtual List<Item> Items { get => field ?? throw new NavigationNotLoadedException(nameof(Items)); set; }
        public virtual List<EquipmentSlot> EquipmentSlots { get => field ?? throw new NavigationNotLoadedException(nameof(EquipmentSlots)); set; }
    }
}