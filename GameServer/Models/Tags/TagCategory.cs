namespace GameServer.Models.Tags
{
    public class TagCategory : IModel
    {
        public int TagCategoryId { get; set; }
        public string TagCategoryName { get; set; }

        public TagCategory(DataAccess.Entities.TagCategories.TagCategory tagCategory)
        {
            TagCategoryId = tagCategory.TagCategoryId;
            TagCategoryName = tagCategory.TagCategoryName;
        }
    }
}
