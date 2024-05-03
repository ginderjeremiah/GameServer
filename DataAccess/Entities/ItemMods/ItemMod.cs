using GameLibrary;
using GameLibrary.Database.Interfaces;
using System.Data;
using System.Text.Json;

namespace DataAccess.Entities.ItemMods
{
    public class ItemMod : IEntity
    {
        public int ItemModId { get; set; }
        public string ItemModName { get; set; }
        public bool Removable { get; set; }
        public string ItemModDesc { get; set; }
        public int SlotTypeId { get; set; }
        public List<ItemModAttribute> Attributes { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            ItemModId = record["ItemModId"].AsInt();
            ItemModName = record["ItemModName"].AsString();
            Removable = record["Removable"].AsBool();
            ItemModDesc = record["ItemModDesc"].AsString();
            SlotTypeId = record["SlotTypeId"].AsInt();
            Attributes = JsonSerializer.Deserialize<List<ItemModAttribute>>(record["AttributesJSON"].AsString()) ?? new List<ItemModAttribute>();
        }
    }
}
