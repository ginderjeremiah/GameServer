namespace Game.Abstractions.Entities
{
    public class UnlockedMod
    {
        public int PlayerId { get; set; }
        public int ItemModId { get; set; }

        public virtual Player Player { get; set; }
        public virtual ItemMod ItemMod { get; set; }
    }
}
