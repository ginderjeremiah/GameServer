using Game.Abstractions;
using Game.Abstractions.Contracts.Admin;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Applies a batch of <see cref="Change{T}"/> records in the canonical admin order and dispatches
    /// each to the matching handler. Changes are processed by descending <see cref="EChangeType"/>
    /// (Delete, then Edit, then Add) so that removals are flushed before insertions, mirroring the
    /// ordering every admin write path relied on.
    /// </summary>
    /// <remarks>
    /// The processor owns the boilerplate (ordering + dispatch) shared by every admin change set; the
    /// supplied handlers own the only part that differs: how an incoming contract maps to a persisted
    /// entity operation. This is an implementation detail of the Content Authoring persistence layer —
    /// it lives in <c>Game.DataAccess</c> alongside the admin repositories, not in <c>Game.Api</c>.
    /// </remarks>
    internal static class ChangeSetProcessor
    {
        public static void Apply<T>(
            IEnumerable<Change<T>> changes,
            Action<T> add,
            Action<T> edit,
            Action<T> delete) where T : IModel
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
                        delete(change.Item);
                        break;
                }
            }
        }
    }
}
