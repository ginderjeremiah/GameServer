namespace GameServer.Models.Request
{
    public class SetTagsData
    {
        public int Id { get; set; }
        public List<int> TagIds { get; set; }
    }
}
