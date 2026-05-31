namespace Game.Api.Models.Common
{
    public class Change<T> : IModel where T : IModel
    {
        public required T Item { get; set; }
        public EChangeType ChangeType { get; set; }
    }
}
