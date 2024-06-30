namespace GameServer.Models.Tags
{
    public class Tag : IModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int TagCategoryId { get; set; }

        public Tag() { }

        public Tag(GameCore.Entities.Tag tag)
        {
            Id = tag.Id;
            Name = tag.Name;
            TagCategoryId = tag.TagCategoryId;
        }
    }
}
