namespace Game.Abstractions.Entities
{
    public class UnlockedItem
    {
        public int PlayerId { get; set; }
        public int ItemId { get; set; }
        public int? EquipmentSlotId { get; set; }

        public virtual Player Player { get; set; }
        public virtual Item Item { get; set; }
    }
}
