using DataAccess.Models.Tags;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class Tags : BaseRepository, ITags
    {
        public Tags(string connectionString) : base(connectionString) { }

        public List<Tag> AllTags()
        {
            var commandText = @"
                SELECT
                    TagId,
                    TagName,
                    TagCategory
                FROM Tags";

            return QueryToList<Tag>(commandText);
        }

        public List<Tag> TagsForItem(int itemId)
        {
            var commandText = @"
                SELECT
                    T.TagId,
                    T.TagName,
                    T.TagCategory
                FROM ItemTags AS IT
                INNER JOIN Tags AS T
                ON T.TagId = IT.TagId
                WHERE ItemId = @ItemId";

            return QueryToList<Tag>(commandText, new SqlParameter("@ItemId", itemId));
        }

        public List<Tag> TagsForItemMod(int itemModId)
        {
            var commandText = @"
                SELECT
                    T.TagId,
                    T.TagName,
                    T.TagCategory
                FROM ItemModTags AS IMT
                INNER JOIN Tags AS T
                ON T.TagId = IMT.TagId
                WHERE ItemModId = @ItemModId";

            return QueryToList<Tag>(commandText, new SqlParameter("@ItemModId", itemModId));
        }

        public void SetItemTags(int itemId, IEnumerable<int> tagIds)
        {
            var commandText = @"
                DELETE ItemTags
                WHERE ItemId = @ItemId

                INSERT INTO ItemTags
                SELECT
                    ItemId = @ItemID,
                    TagId = value
                FROM
                    STRING_SPLIT(@TagIds, ',')";

            var tagIdStr = string.Join(",", tagIds);
            ExecuteNonQuery(commandText, new SqlParameter("@ItemId", itemId), new SqlParameter("@TagIds", tagIdStr));
        }

        public void SetItemModTags(int itemModId, IEnumerable<int> tagIds)
        {
            var commandText = @"
                DELETE ItemModTags
                WHERE ItemModId = @ItemModId

                INSERT INTO ItemModTags
                SELECT
                    ItemModId = @ItemModId,
                    TagId = value
                FROM
                    STRING_SPLIT(@TagIds, ',')";

            var tagIdStr = string.Join(",", tagIds);
            ExecuteNonQuery(commandText, new SqlParameter("@ItemModId", itemModId), new SqlParameter("@TagIds", tagIdStr));
        }

        public void AddTag(string tagName, string tagCategory)
        {
            var commandText = @"
                INSERT INTO Tags
                VALUES
                    (@TagName, @TagCategory)";

            ExecuteNonQuery(commandText, new SqlParameter("@TagName", tagName), new SqlParameter("@TagCategory", tagCategory));
        }

        public void UpdateTag(int tagId, string tagName, string tagCategory)
        {
            var commandText = @"
                UPDATE Tags
                SET TagName = @TagName,
                    TagCategory = @TagCategory
                WHERE TagId = @TagId";

            ExecuteNonQuery(commandText, new SqlParameter("@TagName", tagName), new SqlParameter("@TagCategory", tagCategory), new SqlParameter("@TagId", tagId));
        }

        public void DeleteTag(int tagId)
        {
            var commandText = @"
                DELETE Tags
                WHERE TagId = @TagId";

            ExecuteNonQuery(commandText, new SqlParameter("@TagId", tagId));
        }
    }

    public interface ITags
    {
        public List<Tag> AllTags();
        public List<Tag> TagsForItem(int itemId);
        public List<Tag> TagsForItemMod(int itemModId);
        public void SetItemTags(int itemId, IEnumerable<int> tagIds);
        public void SetItemModTags(int itemModId, IEnumerable<int> tagIds);
        public void AddTag(string tagName, string tagCategory);
        public void UpdateTag(int tagId, string tagName, string tagCategory);
        public void DeleteTag(int tagId);
    }
}
