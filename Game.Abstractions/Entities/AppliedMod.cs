namespace Game.Abstractions.Entities
{
    public class AppliedMod
    {
        public int PlayerId { get; set; }
        public int ItemId { get; set; }
        public int ItemModSlotId { get; set; }
        public int ItemModId { get; set; }

        public virtual Player Player { get => field ?? throw new NavigationNotLoadedException(nameof(Player)); set; }
        public virtual Item Item { get => field ?? throw new NavigationNotLoadedException(nameof(Item)); set; }
        public virtual ItemModSlot ItemModSlot { get => field ?? throw new NavigationNotLoadedException(nameof(ItemModSlot)); set; }
        public virtual ItemMod ItemMod { get => field ?? throw new NavigationNotLoadedException(nameof(ItemMod)); set; }
    }
}
