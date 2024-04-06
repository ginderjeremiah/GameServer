namespace GameServer.Models.Request
{
    public class Change<T> : IModel where T : IModel
    {
        public T Item { get; set; }
        public ChangeType ChangeType { get; set; }
    }

    public enum ChangeType
    {
        Edit = 0,
        Add = 1,
        Delete = 2
    }
}
