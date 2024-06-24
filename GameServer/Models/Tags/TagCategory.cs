namespace GameServer.Models.Tags
{
    public class TagCategory : IModel
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public TagCategory(GameCore.Entities.TagCategory tagCategory)
        {
            Id = tagCategory.Id;
            Name = tagCategory.Name;
        }
    }
}
