using Game.DataAccess.Repositories;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Records the entity-store operations a unit under test stages, so a test can assert exactly which
    /// inserts/updates/deletes were issued without a database. Each list holds the entities passed to the
    /// matching call, in call order.
    /// </summary>
    internal sealed class RecordingEntityStore : IEntityStore
    {
        public List<object> Inserted { get; } = [];
        public List<object> Updated { get; } = [];
        public List<object> Deleted { get; } = [];
        public List<object> Tracked { get; } = [];

        public void Insert<TEntity>(TEntity entity) where TEntity : class => Inserted.Add(entity);
        public void Update<TEntity>(TEntity entity) where TEntity : class => Updated.Add(entity);
        public void Delete<TEntity>(TEntity entity) where TEntity : class => Deleted.Add(entity);
        public void Track<TEntity>(TEntity entity) where TEntity : class => Tracked.Add(entity);
    }
}
