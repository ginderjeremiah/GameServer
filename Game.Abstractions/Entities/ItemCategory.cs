namespace Game.Abstractions.Entities
{
    public partial class ItemCategory
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public virtual List<Item> Items { get; set; }
        public virtual List<EquipmentSlot> EquipmentSlots { get; set; }
    }
}