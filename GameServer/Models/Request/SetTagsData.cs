namespace GameServer.Models.Request
{
    public class SetTagsData : IModel
    {
        public int Id { get; set; }
        public List<int> TagIds { get; set; }
    }
}
