namespace DataAccess.Models.ItemMods
{
    public class ItemMod
    {
        public int ItemModId { get; set; }
        public string ItemModName { get; set; }
        public bool Removable { get; set; }
        public string ItemModDesc { get; set; }
        public int SlotTypeId { get; set; }
    }
}
