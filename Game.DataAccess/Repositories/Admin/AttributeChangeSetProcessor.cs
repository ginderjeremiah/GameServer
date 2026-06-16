using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Applies an attribute-keyed Add/Edit/Delete change set — an item's or item mod's attributes, or a
    /// skill's damage multipliers — to one owner's already-resolved child collection, building a fresh,
    /// navigation-free entity per change. It owns the shape every such setter repeated: an Insert per Add and
    /// a membership-guarded Update/Delete per Edit/Delete. The caller supplies only the part that differs
    /// across the three owners — how a desired <see cref="BattlerAttribute"/> maps to its persisted entity
    /// (the owner FK plus the <c>Amount</c>/<c>Multiplier</c> projection) — via <paramref name="toEntity"/>.
    /// This is an implementation detail of the Content Authoring persistence layer; it lives in
    /// <c>Game.DataAccess</c> alongside <see cref="ChangeSetProcessor"/> and the admin repositories.
    /// </summary>
    /// <remarks>
    /// Edit and Delete are guarded by the owner's current attribute membership: a change targeting an
    /// attribute the owner doesn't have is a no-op, never an EF 0-row update/delete that would throw. This is
    /// deliberately distinct from the identity-level saves (which reject an edit of a missing record up
    /// front): the change set here is a delta the client computed against a baseline it just read, so an
    /// edit/delete of an absent attribute reconciles idempotently to the same end state — see
    /// <c>docs/backend-admin.md</c> → Admin Tools API surface. Ordering and dispatch are delegated to
    /// <see cref="ChangeSetProcessor"/>; the same fresh entity carries the key for a delete (its non-key
    /// payload is ignored once the row is staged as removed).
    /// </remarks>
    internal static class AttributeChangeSetProcessor
    {
        public static void Apply<TEntity>(
            IReadOnlyList<Change<BattlerAttribute>> changes,
            IReadOnlyCollection<TEntity> existing,
            Func<TEntity, int> existingKey,
            Func<BattlerAttribute, TEntity> toEntity,
            IEntityStore entityStore) where TEntity : class
        {
            var existingKeys = existing.Select(existingKey).ToHashSet();

            ChangeSetProcessor.Apply(changes,
                add: attribute => entityStore.Insert(toEntity(attribute)),
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
