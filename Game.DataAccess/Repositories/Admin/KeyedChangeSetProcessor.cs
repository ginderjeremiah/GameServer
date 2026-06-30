using Game.Abstractions;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Applies a composite-key-keyed Add/Edit/Delete change set — an item's or item mod's attributes, a
    /// skill's damage multipliers, or a skill's damage portions — to one owner's already-resolved child
    /// collection, building a fresh, navigation-free entity per change. It owns the shape every such setter
    /// repeated: a membership-guarded upsert per Add (insert when absent, update when the owner already has
    /// the key) and a membership-guarded Update/Delete per Edit/Delete. The caller supplies only the parts
    /// that differ across owners — the change item's key (<paramref name="itemKey"/>), the existing entity's
    /// key (<paramref name="existingKey"/>), and how a desired change maps to its persisted entity
    /// (<paramref name="toEntity"/>, the owner FK plus the value projection). This is an implementation detail
    /// of the Content Authoring persistence layer; it lives in <c>Game.DataAccess</c> alongside
    /// <see cref="ChangeSetProcessor"/> and the admin repositories.
    /// </summary>
    /// <remarks>
    /// The whole batch is rejected up front if it names the same key more than once (across any change types):
    /// unlike the identity-level saves, the key is meaningful for every change — including an Add (a
    /// composite-PK insert) — so a repeated key double-tracks the same row and EF rejects it mid-batch as an
    /// opaque 500. Rejecting first keeps the batch atomic and surfaces an actionable 400, mirroring
    /// <see cref="ChildCollectionReconciler"/>.
    /// <para>
    /// Past the guard, Add/Edit/Delete are each guarded by the owner's current key membership: an Add of a key
    /// the owner already has upserts (rather than duplicate-inserting into a composite-PK violation), and an
    /// Edit/Delete of a key the owner doesn't have is a no-op (never an EF 0-row update/delete that would
    /// throw). This is deliberately distinct from the identity-level saves (which reject an edit of a missing
    /// record up front): the change set here is a delta the client computed against a baseline it just read,
    /// so it reconciles idempotently to the same end state — see <c>docs/backend-admin.md</c> → Admin Tools
    /// API surface. Ordering and dispatch are delegated to <see cref="ChangeSetProcessor"/>; the same fresh
    /// entity carries the key for a delete (its non-key payload is ignored once the row is staged as removed).
    /// </para>
    /// </remarks>
    internal static class KeyedChangeSetProcessor
    {
        public static AdminSaveResult Apply<TItem, TEntity>(
            IReadOnlyList<Change<TItem>> changes,
            IReadOnlyCollection<TEntity> existing,
            Func<TItem, int> itemKey,
            Func<TEntity, int> existingKey,
            Func<TItem, TEntity> toEntity,
            IEntityStore entityStore,
            string resourceName)
            where TItem : IModel
            where TEntity : class
        {
            // The key is meaningful for every change type (an Add is a composite-PK insert), so the dedup
            // spans the whole batch rather than only the value-tracked Edit/Delete subset.
            if (ChangeSetProcessor.HasDuplicateKey(changes, _ => true, item => itemKey(item)))
            {
                return ChangeSetProcessor.DuplicateFailure<TItem>(resourceName);
            }

            var existingKeys = existing.Select(existingKey).ToHashSet();

            return ChangeSetProcessor.Apply(changes,
                // An Add of a key the owner already has is an upsert, not a duplicate composite-PK insert that
                // violates at commit (an Add of one it doesn't have is a fresh insert).
                add: item =>
                {
                    if (existingKeys.Add(itemKey(item)))
                    {
                        entityStore.Insert(toEntity(item));
                    }
                    else
                    {
                        entityStore.Update(toEntity(item));
                    }
                },
                edit: item =>
                {
                    if (existingKeys.Contains(itemKey(item)))
                    {
                        entityStore.Update(toEntity(item));
                    }
                },
                delete: item =>
                {
                    if (existingKeys.Contains(itemKey(item)))
                    {
                        entityStore.Delete(toEntity(item));
                    }
                });
        }
    }
}
