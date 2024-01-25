using DataAccess.Models.ItemMods;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class ItemMods : BaseRepository, IItemMods
    {
        public ItemMods(string connectionString) : base(connectionString) { }

        public List<ItemMod> AllItemMods()
        {
            var commandText = @"
                SELECT
                    ItemModId,
                    ItemModName,
                    Removable,
                    ItemModDesc,
                    SlotTypeId
                FROM ItemMods
                ORDER BY ItemModId";

            return QueryToList<ItemMod>(commandText);
        }

        public Dictionary<int, List<ItemMod>> GetModsForItemBySlot(int itemId)
        {
            var commandText = @"
                SELECT DISTINCT
                    IM.ItemModId,
                    IM.ItemModName,
                    IM.Removable,
                    IM.ItemModDesc,
                    IM.SlotTypeId
                FROM ItemMods AS IM
                INNER JOIN ItemModTags AS IMT
                ON IM.ItemModId = IMT.ItemModId
                INNER JOIN ItemTags AS IT
                ON IT.TagId = IMT.TagId
                INNER JOIN Items as I
                ON I.ItemId = IT.ItemId
                WHERE I.ItemId = @ItemId";

            return QueryToList<ItemMod>(commandText, new SqlParameter("@ItemId", itemId))
                    .GroupBy(mod => mod.SlotTypeId)
                    .ToDictionary(g => g.Key, g => g.ToList());
        }

        public void AddItemMod(string itemModName, bool removable, string itemModDesc)
        {
            var commandText = @"
                INSERT INTO ItemMods
                VALUES
                    (@ItemModName, @Removable, @ItemModDesc)";

            ExecuteNonQuery(commandText, new SqlParameter("@ItemModName", itemModName), new SqlParameter("@Removable", removable), new SqlParameter("@ItemModDesc", itemModDesc));
        }
        public void UpdateItemMod(int itemModId, string itemModName, bool removable, string itemModDesc)
        {
            var commandText = @"
                UPDATE ItemMods
                SET ItemModName = @ItemModName,
                    Removable = @Removable,
                    ItemModDesc = @ItemModDesc
                WHERE ItemModId = @ItemModId";

            ExecuteNonQuery(commandText,
                            new SqlParameter("@ItemModName", itemModName),
                            new SqlParameter("@Removable", removable),
                            new SqlParameter("@ItemModDesc", itemModDesc),
                            new SqlParameter("@ItemModId", itemModId)
                        );
        }
        public void DeleteItemMod(int itemModId)
        {
            var commandText = @"
                DELETE ItemMods
                WHERE ItemModId = @ItemModId";

            ExecuteNonQuery(commandText, new SqlParameter("@ItemModId", itemModId));
        }
    }

    public interface IItemMods
    {
        public List<ItemMod> AllItemMods();
        public Dictionary<int, List<ItemMod>> GetModsForItemBySlot(int itemId);
        public void AddItemMod(string itemModName, bool removable, string itemModDesc);
        public void UpdateItemMod(int itemModId, string itemModName, bool removable, string itemModDesc);
        public void DeleteItemMod(int itemModId);
    }
}
