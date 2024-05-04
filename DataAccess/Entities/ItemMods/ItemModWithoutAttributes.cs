using GameCore;
using GameCore.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.ItemMods
{
    public class ItemModWithoutAttributes : IEntity
    {
        public int ItemModId { get; set; }
        public string ItemModName { get; set; }
        public bool Removable { get; set; }
        public string ItemModDesc { get; set; }
        public int SlotTypeId { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            ItemModId = record["ItemModId"].AsInt();
            ItemModName = record["ItemModName"].AsString();
            Removable = record["Removable"].AsBool();
            ItemModDesc = record["ItemModDesc"].AsString();
            SlotTypeId = record["SlotTypeId"].AsInt();
        }
    }
}
