namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Reconciles a child collection against the full desired set an admin "set the whole related collection"
    /// endpoint submits: deletes the children no longer wanted, applies the update for those still present, and
    /// inserts the new ones. It owns the three-phase diff (and its <see cref="HashSet{T}"/> membership lookups)
    /// shared by every such setter; the caller supplies only how each phase maps to a persisted entity
    /// operation. This is an implementation detail of the Content Authoring persistence layer — it lives in
    /// <c>Game.DataAccess</c> alongside <see cref="ChangeSetProcessor"/> and the admin repositories.
    /// </summary>
    /// <remarks>
    /// Keys are matched between the existing entities and the desired contracts through two selectors, since the
    /// two sides rarely share a CLR type (an entity's <c>int</c> id vs. a contract's enum, say). The phases:
    /// an existing child whose key is absent from the desired set is deleted; a desired item whose key already
    /// exists is an update; a desired item whose key is new is an insert — so the delete and insert key sets are
    /// disjoint and the phase ordering never double-tracks a key.
    /// <para>
    /// The <paramref name="delete"/> handler receives the <em>existing</em> entity (to read its key) rather than
    /// the helper removing it directly: that entity comes from the <c>AsNoTracking</c> reference cache with a
    /// loaded back-reference graph, so the caller must build a fresh, navigation-free entity to delete (see
    /// <c>docs/backend.md</c> → Admin Tools). The <paramref name="update"/> handler is optional — a pure join row
    /// (e.g. <c>EnemySkill</c>) carries no payload beyond its key, so a child present on both sides needs no update.
    /// </para>
    /// </remarks>
    internal static class ChildCollectionReconciler
    {
        public static void Reconcile<TExisting, TDesired, TKey>(
            IEnumerable<TExisting> existing,
            IEnumerable<TDesired> desired,
            Func<TExisting, TKey> existingKey,
            Func<TDesired, TKey> desiredKey,
            Action<TExisting> delete,
            Action<TDesired> insert,
            Action<TDesired>? update = null) where TKey : notnull
        {
            var desiredKeys = desired.Select(desiredKey).ToHashSet();
            var existingKeys = existing.Select(existingKey).ToHashSet();

            foreach (var child in existing)
            {
                if (!desiredKeys.Contains(existingKey(child)))
                {
                    delete(child);
                }
            }

            foreach (var item in desired)
            {
                if (existingKeys.Contains(desiredKey(item)))
                {
                    update?.Invoke(item);
                }
                else
                {
                    insert(item);
                }
            }
        }
    }
}
