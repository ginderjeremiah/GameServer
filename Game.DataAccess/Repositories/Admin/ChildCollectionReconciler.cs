using Game.Abstractions.DataAccess.Admin;

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
    /// <c>docs/backend-admin.md</c> → Admin Tools). The <paramref name="update"/> handler is optional — a pure join row
    /// (e.g. <c>EnemySkill</c>) carries no payload beyond its key, so a child present on both sides needs no update.
    /// </para>
    /// </remarks>
    internal static class ChildCollectionReconciler
    {
        // The collections are typed IReadOnlyCollection rather than IEnumerable because each is enumerated
        // twice (once to build its key set, once to diff), so a deferred/lazy source would re-run — and could
        // yield a different sequence the second time. Requiring a materialized collection rules that out.
        public static AdminSaveResult Reconcile<TExisting, TDesired, TKey>(
            IReadOnlyCollection<TExisting> existing,
            IReadOnlyCollection<TDesired> desired,
            Func<TExisting, TKey> existingKey,
            Func<TDesired, TKey> desiredKey,
            Action<TExisting> delete,
            Action<TDesired> insert,
            string resourceName,
            Action<TDesired>? update = null) where TKey : notnull
        {
            // A full desired set must not name the same child twice: a duplicate key slips past the
            // existing-membership check (both copies miss it) and double-inserts into a unique violation
            // at commit — or, for an existing key, double-tracks the same entity as Modified, which EF
            // rejects. The set is malformed input, so reject the whole write up front as a business error.
            var desiredKeys = new HashSet<TKey>();
            foreach (var item in desired)
            {
                if (!desiredKeys.Add(desiredKey(item)))
                {
                    // Route through the shared helper so the duplicate-rejection wording can't drift from the
                    // change-set processors'. resourceName is always supplied here, so the type arg is unused.
                    return ChangeSetProcessor.DuplicateFailure<TDesired>(resourceName);
                }
            }

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

            return AdminSaveResult.Success;
        }
    }
}
