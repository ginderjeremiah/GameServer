namespace Game.Infrastructure.Entities
{
    public class UnlockedMod
    {
        public int PlayerId { get; set; }
        public int ItemModId { get; set; }

        public virtual Player Player { get => field ?? throw new NotLoadedException(nameof(Player)); set; }
        public virtual ItemMod ItemMod { get => field ?? throw new NotLoadedException(nameof(ItemMod)); set; }
    }
}
