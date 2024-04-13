using GameLibrary;
using System.Data.SqlClient;

namespace DataAccess.Entities.ItemMods
{
    public class ItemModWithoutAttributes : IEntity
    {
        public int ItemModId { get; set; }
        public string ItemModName { get; set; }
        public bool Removable { get; set; }
        public string ItemModDesc { get; set; }
        public int SlotTypeId { get; set; }

        public void LoadFromReader(SqlDataReader reader)
        {
            ItemModId = reader["ItemModId"].AsInt();
            ItemModName = reader["ItemModName"].AsString();
            Removable = reader["Removable"].AsBool();
            ItemModDesc = reader["ItemModDesc"].AsString();
            SlotTypeId = reader["SlotTypeId"].AsInt();
        }
    }
}
