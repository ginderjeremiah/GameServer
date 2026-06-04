using Game.Api.Models;
using Game.Api.Models.Common;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Applies a batch of <see cref="Change{T}"/> records in the canonical admin order and
    /// dispatches each to the matching handler. Changes are processed by descending
    /// <see cref="EChangeType"/> (Delete, then Edit, then Add) so that removals are flushed before
    /// insertions, mirroring the ordering every admin <c>AddEdit*</c> endpoint relied on.
    /// </summary>
    /// <remarks>
    /// The processor owns the boilerplate (ordering + dispatch) that every admin change endpoint
    /// shared; the supplied handlers own the only part that differs between endpoints: how an
    /// incoming model maps to a persisted entity operation.
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
