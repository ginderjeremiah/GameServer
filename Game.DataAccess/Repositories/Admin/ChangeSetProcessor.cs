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
    /// </remarks>
    internal static class ChangeSetProcessor
    {
        public static AdminSaveResult Apply<T>(
            IEnumerable<Change<T>> changes,
            Action<T> add,
            Action<T> edit,
            Action<T>? delete = null) where T : IModel
        {
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
    }
}
