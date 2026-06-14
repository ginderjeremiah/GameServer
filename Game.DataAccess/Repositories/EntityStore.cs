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
    }
}
