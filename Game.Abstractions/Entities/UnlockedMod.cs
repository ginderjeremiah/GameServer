namespace Game.Abstractions.Entities
{
    public class UnlockedMod
    {
        public int PlayerId { get; set; }
        public int ItemModId { get; set; }

        public virtual Player Player { get => field ?? throw new NavigationNotLoadedException(nameof(Player)); set; }
        public virtual ItemMod ItemMod { get => field ?? throw new NavigationNotLoadedException(nameof(ItemMod)); set; }
    }
}
