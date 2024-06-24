namespace GameCore.Entities
{
    public class SlotType
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public virtual List<ItemMod> ItemMods { get; set; }
        public virtual List<ItemSlot> ItemSlots { get; set; }
    }
}
