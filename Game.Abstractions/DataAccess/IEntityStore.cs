namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Provides low-level entity change-tracking operations (insert, update, delete).
    /// Changes are persisted when <see cref="IUnitOfWork.CommitAsync"/> is called.
    /// </summary>
    public interface IEntityStore
    {
        void Insert<TEntity>(TEntity entity) where TEntity : class;
        void InsertAll<TEntity>(IEnumerable<TEntity> entities) where TEntity : class;
        void Delete<TEntity>(TEntity entity) where TEntity : class;
        void Update<TEntity>(TEntity entity) where TEntity : class;
    }
}
