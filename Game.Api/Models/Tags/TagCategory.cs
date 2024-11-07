using TagCategoryEntity = Game.Core.Entities.TagCategory;

namespace Game.Api.Models.Tags
{
    public class TagCategory : IModelFromSource<TagCategory, TagCategoryEntity>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public static TagCategory FromSource(TagCategoryEntity tagCategory)
        {
            return new TagCategory
            {
                Id = tagCategory.Id,
                Name = tagCategory.Name,
            };
        }
    }
}
