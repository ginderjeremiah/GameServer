using DataAccess.Entities.Tags;
using GameLibrary.Database;
using GameLibrary.Database.Interfaces;

namespace DataAccess.Repositories
{
    internal class Tags : BaseRepository, ITags
    {
        public Tags(IDataProvider database) : base(database) { }

        public List<Tag> AllTags()
        {
            var commandText = @"
                SELECT
                    TagId,
                    TagName,
                    TagCategoryId
                FROM Tags";

            return Database.QueryToList<Tag>(commandText);
        }

        public List<Tag> TagsForItem(int itemId)
        {
            var commandText = @"
                SELECT
                    T.TagId,
                    T.TagName,
                    T.TagCategoryId
                FROM ItemTags AS IT
                INNER JOIN Tags AS T
                ON T.TagId = IT.TagId
                WHERE ItemId = @ItemId";

            return Database.QueryToList<Tag>(commandText, new QueryParameter("@ItemId", itemId));
        }

        public List<Tag> TagsForItemMod(int itemModId)
        {
            var commandText = @"
                SELECT
                    T.TagId,
                    T.TagName,
                    T.TagCategoryId
                FROM ItemModTags AS IMT
                INNER JOIN Tags AS T
                ON T.TagId = IMT.TagId
                WHERE ItemModId = @ItemModId";

            return Database.QueryToList<Tag>(commandText, new QueryParameter("@ItemModId", itemModId));
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
            Database.ExecuteNonQuery(commandText, new QueryParameter("@ItemId", itemId), new QueryParameter("@TagIds", tagIdStr));
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
            Database.ExecuteNonQuery(commandText, new QueryParameter("@ItemModId", itemModId), new QueryParameter("@TagIds", tagIdStr));
        }

        public void AddTag(string tagName, int tagCategoryId)
        {
            var commandText = @"
                INSERT INTO Tags
                VALUES
                    (@TagName, @TagCategoryId)";

            Database.ExecuteNonQuery(commandText, new QueryParameter("@TagName", tagName), new QueryParameter("@TagCategoryId", tagCategoryId));
        }

        public void UpdateTag(int tagId, string tagName, int tagCategoryId)
        {
            var commandText = @"
                UPDATE Tags
                SET TagName = @TagName,
                    TagCategoryId = @TagCategoryId
                WHERE TagId = @TagId";

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@TagName", tagName),
                new QueryParameter("@TagCategoryId", tagCategoryId),
                new QueryParameter("@TagId", tagId)
            );
        }

        public void DeleteTag(int tagId)
        {
            var commandText = @"
                DELETE Tags
                WHERE TagId = @TagId";

            Database.ExecuteNonQuery(commandText, new QueryParameter("@TagId", tagId));
        }
    }

    public interface ITags
    {
        public List<Tag> AllTags();
        public List<Tag> TagsForItem(int itemId);
        public List<Tag> TagsForItemMod(int itemModId);
        public void SetItemTags(int itemId, IEnumerable<int> tagIds);
        public void SetItemModTags(int itemModId, IEnumerable<int> tagIds);
        public void AddTag(string tagName, int tagCategoryId);
        public void UpdateTag(int tagId, string tagName, int tagCategoryId);
        public void DeleteTag(int tagId);
    }
}
