namespace GameServer.Models.Common
{
    public class Change<T> : IModel where T : IModel
    {
        public T Item { get; set; }
        public ChangeType ChangeType { get; set; }
    }
}
