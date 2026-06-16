namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Provides low-level entity change-tracking operations (insert, update, delete).
    /// Changes are persisted when <see cref="IUnitOfWork.CommitAsync"/> is called.
    /// </summary>
    internal interface IEntityStore
    {
        void Insert<TEntity>(TEntity entity) where TEntity : class;
        void Delete<TEntity>(TEntity entity) where TEntity : class;

        /// <summary>
        /// Stages a delete for the row identified by <paramref name="keyValues"/> without an entity instance,
        /// so a record whose only non-key state is a <c>required</c> scalar (e.g. <c>Tag.Name</c>) is removed
        /// by key alone — no unrelated columns are fabricated to satisfy the initializer. The delete stays in
        /// the change-tracker batch and is persisted with the rest of the set on <see cref="IUnitOfWork.CommitAsync"/>.
        /// </summary>
        void DeleteByKey<TEntity>(params object[] keyValues) where TEntity : class;
        void Update<TEntity>(TEntity entity) where TEntity : class;
        void Track<TEntity>(TEntity entity) where TEntity : class;
    }
}
