namespace GameServer.Models.Tags
{
    public class Tag : IModel
    {
        public int TagId { get; set; }
        public string TagName { get; set; }
        public string TagCategory { get; set; }

        public Tag(DataAccess.Models.Tags.Tag tag)
        {
            TagId = tag.TagId;
            TagName = tag.TagName;
            TagCategory = tag.TagCategory;
        }
    }
}
