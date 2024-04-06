namespace GameServer.Models.Items
{
    public class ItemMod : IModel
    {
        public int ItemModId { get; set; }
        public string ItemModName { get; set; }
        public bool Removable { get; set; }
        public string ItemModDesc { get; set; }
        public int SlotTypeId { get; set; }

        public ItemMod(DataAccess.Models.ItemMods.ItemMod itemMod)
        {
            ItemModId = itemMod.ItemModId;
            ItemModName = itemMod.ItemModName;
            Removable = itemMod.Removable;
            ItemModDesc = itemMod.ItemModDesc;
            SlotTypeId = itemMod.SlotTypeId;
        }
    }
}
