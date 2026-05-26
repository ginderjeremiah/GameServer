namespace Game.Abstractions.Entities
{
    public partial class ItemModType
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public virtual List<ItemMod> ItemMods { get => field ?? throw new NavigationNotLoadedException(nameof(ItemMods)); set; }
        public virtual List<ItemModSlot> ItemModSlots { get => field ?? throw new NavigationNotLoadedException(nameof(ItemModSlots)); set; }
    }
}
