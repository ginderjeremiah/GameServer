namespace Game.Abstractions.Entities
{
    public partial class EquipmentSlot
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int ItemCategoryId { get; set; }

        public virtual ItemCategory ItemCategory { get => field ?? throw new NavigationNotLoadedException(nameof(ItemCategory)); set; }
    }
}
