namespace Game.Abstractions.Entities
{
    public partial class ItemModSlot
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public int ItemModSlotTypeId { get; set; }
        public int Index { get; set; }

        public virtual Item Item { get => field ?? throw new NavigationNotLoadedException(nameof(Item)); set; }
        public virtual ItemModType ItemModSlotType { get => field ?? throw new NavigationNotLoadedException(nameof(ItemModSlotType)); set; }
    }
}
