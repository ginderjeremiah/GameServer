namespace GameServer.Models.Tags
{
    public class Tag : IModel
    {
        public int TagId { get; set; }
        public string TagName { get; set; }
        public int TagCategoryId { get; set; }

        public Tag(DataAccess.Entities.Tags.Tag tag)
        {
            TagId = tag.TagId;
            TagName = tag.TagName;
            TagCategoryId = tag.TagCategoryId;
        }
    }
}
