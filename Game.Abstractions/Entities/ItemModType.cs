namespace Game.Abstractions.Entities
{
    public partial class ItemModType
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public virtual List<ItemMod> ItemMods { get; set; }
        public virtual List<ItemModSlot> ItemModSlots { get; set; }
    }
}
