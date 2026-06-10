namespace Game.Infrastructure.Entities
{
    public class ItemModSlot
    {
        public int Id { get; set; }
        public int ItemId { get; set; }
        public int ItemModSlotTypeId { get; set; }

        public virtual Item Item { get => field ?? throw new NotLoadedException(nameof(Item)); set; }
        public virtual ItemModType ItemModSlotType { get => field ?? throw new NotLoadedException(nameof(ItemModSlotType)); set; }
    }
}
