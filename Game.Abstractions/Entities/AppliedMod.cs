namespace Game.Abstractions.Entities
{
    public class AppliedMod
    {
        public int PlayerId { get; set; }
        public int ItemId { get; set; }
        public int ItemModSlotId { get; set; }
        public int ItemModId { get; set; }

        public virtual Player Player { get; set; }
        public virtual Item Item { get; set; }
        public virtual ItemModSlot ItemModSlot { get; set; }
        public virtual ItemMod ItemMod { get; set; }
    }
}
