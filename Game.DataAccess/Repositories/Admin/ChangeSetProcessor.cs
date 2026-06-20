using Game.Abstractions;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Applies a batch of <see cref="Change{T}"/> records in the canonical admin order and dispatches
    /// each to the matching handler. Changes are processed by descending <see cref="EChangeType"/>
    /// (Delete, then Edit, then Add) so that removals are flushed before insertions, mirroring the
    /// ordering every admin write path relied on. The outcome flows back as an <see cref="AdminSaveResult"/>,
    /// the unified result every admin write reports through.
    /// </summary>
    /// <remarks>
    /// The processor owns the boilerplate (ordering + dispatch) shared by every admin change set; the
    /// supplied handlers own the only part that differs: how an incoming contract maps to a persisted
    /// entity operation. This is an implementation detail of the Content Authoring persistence layer —
    /// it lives in <c>Game.DataAccess</c> alongside the admin repositories, not in <c>Game.Api</c>.
    /// <para>
    /// The <c>delete</c> handler is optional: the zero-based-id reference records (items, item mods, skills,
    /// enemies, zones, challenges) are <em>retired</em>, not deleted (a hard delete would open an Id gap that
    /// silently mis-resolves index-based lookups — see <c>docs/backend.md</c> → Reference Data), so they omit
    /// it. A <see cref="EChangeType.Delete"/> against such a set is a client input error, not a server fault,
    /// so it is rejected as a graceful business failure (a <see cref="AdminSaveResult"/> the controller maps
    /// to a 400) rather than thrown — leaving the batch atomic and surfacing an actionable message instead of
    /// an opaque 500.
    /// </para>
    /// <para>
    /// When <paramref name="key"/> is supplied the batch is rejected up front if it names the same value-tracked
    /// key more than once — mirroring <see cref="ChildCollectionReconciler"/>. Two Edits (or an Edit and a
    /// Delete) of the same key map to two Update/Delete ops on distinct CLR instances sharing that key, which EF
    /// double-tracks and rejects mid-batch as an opaque 500. Adds are excluded from this guard: an insert's wire
    /// key is a store-generated sentinel (every new row's id resolves on commit), so two Adds never collide on it
    /// and deduping them would falsely reject distinct new records.
    /// </para>
    /// </remarks>
    internal static class ChangeSetProcessor
    {
        public static AdminSaveResult Apply<T>(
            IEnumerable<Change<T>> changes,
            Action<T> add,
            Action<T> edit,
            Action<T>? delete = null,
            Func<T, object>? key = null,
            string? resourceName = null) where T : IModel
        {
            // Only Edit/Delete are value-tracked (Adds get a store-generated key), so they alone can collide.
            if (key is not null
                && HasDuplicateKey(changes, c => c.ChangeType != EChangeType.Add, key))
            {
                return DuplicateFailure<T>(resourceName);
            }

            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                switch (change.ChangeType)
                {
                    case EChangeType.Add:
                        add(change.Item);
                        break;
                    case EChangeType.Edit:
                        edit(change.Item);
                        break;
                    case EChangeType.Delete:
                        if (delete is null)
                        {
                            // Deletes sort first, so nothing has been applied yet — reject the whole batch
                            // up front as a business error rather than throwing for what is validated input.
                            return AdminSaveResult.Failure(
                                $"Delete is not supported for {typeof(T).Name}: reference records are retired, not deleted.");
                        }
                        delete(change.Item);
                        break;
                }
            }

            return AdminSaveResult.Success;
        }

        /// <summary>
        /// True when the <paramref name="participates"/> subset of <paramref name="changes"/> names the same
        /// key (per <paramref name="key"/>) more than once. Shared by the change-set processors so the
        /// "a key must not be named twice in one batch" guard lives in one place.
        /// </summary>
        public static bool HasDuplicateKey<T>(
            IEnumerable<Change<T>> changes,
            Func<Change<T>, bool> participates,
            Func<T, object> key) where T : IModel
        {
            var seen = new HashSet<object>();
            foreach (var change in changes)
            {
                if (participates(change) && !seen.Add(key(change.Item)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The unified duplicate-key rejection message, shared by every change-set processor.</summary>
        public static AdminSaveResult DuplicateFailure<T>(string? resourceName) =>
            AdminSaveResult.Failure(
                $"The submitted {resourceName ?? typeof(T).Name} change set contains duplicate entries.");
    }
}
