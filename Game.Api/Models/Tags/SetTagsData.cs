namespace Game.Api.Models.Tags
{
    public class SetTagsData : IModel
    {
        public int Id { get; set; }
        public required List<int> TagIds { get; set; }
    }
}
