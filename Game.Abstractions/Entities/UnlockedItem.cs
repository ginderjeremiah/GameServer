namespace Game.Abstractions.Entities
{
    public class UnlockedItem
    {
        public int PlayerId { get; set; }
        public int ItemId { get; set; }
        public int? EquipmentSlotId { get; set; }

        public virtual Player Player { get => field ?? throw new NavigationNotLoadedException(nameof(Player)); set; }
        public virtual Item Item { get => field ?? throw new NavigationNotLoadedException(nameof(Item)); set; }
    }
}
