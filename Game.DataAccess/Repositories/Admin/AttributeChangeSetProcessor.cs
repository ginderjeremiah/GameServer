using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Applies an attribute-keyed Add/Edit/Delete change set — an item's or item mod's attributes, or a
    /// skill's damage multipliers — to one owner's already-resolved child collection, building a fresh,
    /// navigation-free entity per change. It owns the shape every such setter repeated: a membership-guarded
    /// upsert per Add (insert when absent, update when the owner already has the attribute) and a
    /// membership-guarded Update/Delete per Edit/Delete. The caller supplies only the part that differs
    /// across the three owners — how a desired <see cref="BattlerAttribute"/> maps to its persisted entity
    /// (the owner FK plus the <c>Amount</c>/<c>Multiplier</c> projection) — via <paramref name="toEntity"/>.
    /// This is an implementation detail of the Content Authoring persistence layer; it lives in
    /// <c>Game.DataAccess</c> alongside <see cref="ChangeSetProcessor"/> and the admin repositories.
    /// </summary>
    /// <remarks>
    /// The whole batch is rejected up front if it names the same <see cref="EAttribute"/> more than once
    /// (across any change types): unlike the identity-level saves, an attribute key is meaningful for every
    /// change — including an Add (a composite-PK insert) — so a repeated key double-tracks the same row and EF
    /// rejects it mid-batch as an opaque 500. Rejecting first keeps the batch atomic and surfaces an actionable
    /// 400, mirroring <see cref="ChildCollectionReconciler"/>.
    /// <para>
    /// Past the guard, Add/Edit/Delete are each guarded by the owner's current attribute membership: an Add of
    /// an attribute the owner already has upserts (rather than duplicate-inserting into a composite-PK
    /// violation), and an Edit/Delete of an attribute the owner doesn't have is a no-op (never an EF 0-row
    /// update/delete that would throw). This is deliberately distinct from the identity-level saves (which
    /// reject an edit of a missing record up front): the change set here is a delta the client computed against
    /// a baseline it just read, so it reconciles idempotently to the same end state — see
    /// <c>docs/backend-admin.md</c> → Admin Tools API surface. Ordering and dispatch are delegated to
    /// <see cref="ChangeSetProcessor"/>; the same fresh entity carries the key for a delete (its non-key
    /// payload is ignored once the row is staged as removed).
    /// </para>
    /// </remarks>
    internal static class AttributeChangeSetProcessor
    {
        public static AdminSaveResult Apply<TEntity>(
            IReadOnlyList<Change<BattlerAttribute>> changes,
            IReadOnlyCollection<TEntity> existing,
            Func<TEntity, int> existingKey,
            Func<BattlerAttribute, TEntity> toEntity,
            IEntityStore entityStore,
            string resourceName) where TEntity : class
        {
            // An attribute key is meaningful for every change type (an Add is a composite-PK insert), so the
            // dedup spans the whole batch rather than only the value-tracked Edit/Delete subset.
            if (ChangeSetProcessor.HasDuplicateKey(changes, _ => true, a => a.AttributeId))
            {
                return ChangeSetProcessor.DuplicateFailure<BattlerAttribute>(resourceName);
            }

            var existingKeys = existing.Select(existingKey).ToHashSet();

            return ChangeSetProcessor.Apply(changes,
                // An Add of an attribute the owner already has is an upsert, not a duplicate composite-PK
                // insert that violates at commit (an Add of one it doesn't have is a fresh insert).
                add: attribute =>
                {
                    if (existingKeys.Add((int)attribute.AttributeId))
                    {
                        entityStore.Insert(toEntity(attribute));
                    }
                    else
                    {
                        entityStore.Update(toEntity(attribute));
                    }
                },
                edit: attribute =>
                {
                    if (existingKeys.Contains((int)attribute.AttributeId))
                    {
                        entityStore.Update(toEntity(attribute));
                    }
                },
                delete: attribute =>
                {
                    if (existingKeys.Contains((int)attribute.AttributeId))
                    {
                        entityStore.Delete(toEntity(attribute));
                    }
                });
        }
    }
}
