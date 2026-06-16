using System.Runtime.CompilerServices;
using Game.Abstractions.DataAccess;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class EntityStore(GameContext context) : IEntityStore
    {
        private readonly GameContext _context = context;

        public void Insert<TEntity>(TEntity entity) where TEntity : class
        {
            _context.Add(entity);
        }

        public void Delete<TEntity>(TEntity entity) where TEntity : class
        {
            var entry = _context.Entry(entity);
            if (entry.State is EntityState.Detached)
            {
                entry.State = EntityState.Unchanged;
            }

            _context.Remove(entity);
        }

        public void DeleteByKey<TEntity>(params object[] keyValues) where TEntity : class
        {
            Delete(BuildKeyOnlyStub<TEntity>(keyValues));
        }

        public void Update<TEntity>(TEntity entity) where TEntity : class
        {
            // Mark just this entity as Modified rather than calling _context.Update, which would
            // (1) walk the navigation graph and drag in — then potentially re-insert — any related
            // entities still attached to a detached/cached entity, and (2) infer Added vs. Modified
            // from the key value, wrongly treating our zero-based identity rows (Id == 0, the
            // identity column's seed) as new and inserting a duplicate. Setting the state directly
            // emits a single-row UPDATE for exactly this entity, regardless of its key value.
            _context.Entry(entity).State = EntityState.Modified;
        }

        public void Track<TEntity>(TEntity entity) where TEntity : class
        {
            _context.Entry(entity).State = EntityState.Unchanged;
        }

        // Builds a stub carrying only the primary key. The instance is created uninitialized — `required`
        // is a compile-time guard the runtime doesn't enforce, the same way EF's own materializer
        // constructs entities — so a key-only delete never has to fabricate a value for an unrelated
        // required scalar. EF reads just the key for the resulting DELETE; the unset non-key columns
        // (including any unset navigation) are never touched, mirroring the existing entity-instance Delete.
        private TEntity BuildKeyOnlyStub<TEntity>(object[] keyValues) where TEntity : class
        {
            var entityType = _context.Model.FindEntityType(typeof(TEntity))
                ?? throw new InvalidOperationException($"'{typeof(TEntity).Name}' is not a mapped entity type.");
            var keyProperties = (entityType.FindPrimaryKey()
                ?? throw new InvalidOperationException($"'{typeof(TEntity).Name}' has no primary key to delete by.")).Properties;

            if (keyValues.Length != keyProperties.Count)
            {
                throw new ArgumentException(
                    $"'{typeof(TEntity).Name}' has a {keyProperties.Count}-column primary key but {keyValues.Length} value(s) were supplied.",
                    nameof(keyValues));
            }

            var stub = (TEntity)RuntimeHelpers.GetUninitializedObject(typeof(TEntity));
            for (var i = 0; i < keyProperties.Count; i++)
            {
                var keyProperty = keyProperties[i].PropertyInfo
                    ?? throw new InvalidOperationException(
                        $"Key property '{keyProperties[i].Name}' on '{typeof(TEntity).Name}' has no CLR property to set.");
                keyProperty.SetValue(stub, keyValues[i]);
            }

            return stub;
        }
    }
}
