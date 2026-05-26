namespace Game.Abstractions.Entities
{
    public partial class Item : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public int ItemCategoryId { get; set; }
        public required string IconPath { get; set; }

        public virtual List<ItemAttribute> ItemAttributes { get => field ?? throw new NavigationNotLoadedException(nameof(ItemAttributes)); set; }
        public virtual ItemCategory ItemCategory { get => field ?? throw new NavigationNotLoadedException(nameof(ItemCategory)); set; }
        public virtual List<ItemModSlot> ItemModSlots { get => field ?? throw new NavigationNotLoadedException(nameof(ItemModSlots)); set; }
        public virtual List<Tag> Tags { get => field ?? throw new NavigationNotLoadedException(nameof(Tags)); set; }
        public virtual List<UnlockedItem> UnlockedItems { get => field ?? throw new NavigationNotLoadedException(nameof(UnlockedItems)); set; }
    }
}
