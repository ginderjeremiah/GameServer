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

        public void InsertAll<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
        {
            _context.AddRange(entities);
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
            var entry = _context.Update(entity);
            if (entry.State is not EntityState.Added)
            {
                var idProp = entry.Properties.FirstOrDefault(p => p.IsTemporary && p.Metadata.IsPrimaryKey());
                if (idProp is not null)
                {
                    idProp.IsTemporary = false;
                    idProp.CurrentValue = 0;
                    entry.State = EntityState.Modified;
                }
            }
        }
    }
}
