using TagEntity = Game.Core.Entities.Tag;

namespace Game.Api.Models.Tags
{
    public class Tag : IModelFromSource<Tag, TagEntity>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int TagCategoryId { get; set; }

        public static Tag FromSource(TagEntity tag)
        {
            return new Tag
            {
                Id = tag.Id,
                Name = tag.Name,
                TagCategoryId = tag.TagCategoryId,
            };
        }
    }
}
