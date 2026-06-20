using System.Linq.Expressions;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    /// <summary>
    /// Shared building blocks for the write-behind player-update handlers, factored out so the idempotency
    /// contract these handlers depend on lives — and is tested — in one place rather than copy-pasted.
    /// </summary>
    internal static class PlayerUpdateHandlerExtensions
    {
        /// <summary>
        /// Inserts <paramref name="factory"/>'s row only if no row matching <paramref name="existsPredicate"/>
        /// is present, idempotent under the queue's at-least-once read. The existence check skips the common
        /// re-apply without touching the row (no tracking needed on this hot-path read) but isn't atomic with
        /// the insert: a concurrent apply of the same event can insert the row between the check and the save,
        /// so the unique-violation catch absorbs that race as a benign no-op — the table's unique key already
        /// holds the row. Re-applying therefore always converges to a single row.
        /// </summary>
        public static async Task InsertIfMissingAsync<TEntity>(
            this GameContext context,
            Expression<Func<TEntity, bool>> existsPredicate,
            Func<TEntity> factory)
            where TEntity : class
        {
            var exists = await context.Set<TEntity>()
                .AsNoTracking()
                .AnyAsync(existsPredicate);

            if (exists)
            {
                return;
            }

            context.Set<TEntity>().Add(factory());

            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                // A concurrent apply inserted the same row first; it already exists, so this is a no-op.
            }
        }

        /// <summary>
        /// Indexes <paramref name="source"/> by <paramref name="keySelector"/>, taking the first row per key.
        /// Group-by-first rather than <c>ToDictionary</c>: the table's unique key makes a duplicate key
        /// impossible, but taking the first keeps a stray duplicate row from throwing here and poisoning the
        /// player's update stream.
        /// </summary>
        public static Dictionary<TKey, TSource> ToFirstByKey<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector)
            where TKey : notnull
        {
            return source
                .GroupBy(keySelector)
                .ToDictionary(g => g.Key, g => g.First());
        }
    }
}
