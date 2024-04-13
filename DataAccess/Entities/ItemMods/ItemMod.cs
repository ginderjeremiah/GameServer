using GameLibrary;
using System.Data.SqlClient;
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

        public void LoadFromReader(SqlDataReader reader)
        {
            ItemModId = reader["ItemModId"].AsInt();
            ItemModName = reader["ItemModName"].AsString();
            Removable = reader["Removable"].AsBool();
            ItemModDesc = reader["ItemModDesc"].AsString();
            SlotTypeId = reader["SlotTypeId"].AsInt();
            Attributes = JsonSerializer.Deserialize<List<ItemModAttribute>>(reader["AttributesJSON"].AsString());
        }
    }
}
