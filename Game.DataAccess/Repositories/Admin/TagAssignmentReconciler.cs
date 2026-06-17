namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Reconciles one owner's tag-assignment join rows (an item's <c>ItemTag</c>s or an item mod's
    /// <c>ItemModTag</c>s) against the desired set an admin tag-setting endpoint submits. It reads the owner's
    /// current tag ids and the subset of desired ids that actually exist, then inserts/deletes a single
    /// navigation-free join row per difference — never loading a tag's full item / item-mod membership. The
    /// caller supplies only how a tag id maps to its join row via <paramref name="toJoinRow"/>; the membership
    /// diff is delegated to <see cref="ChildCollectionReconciler"/>. A join row is a pure key (no payload), so
    /// there is no update phase — an id present on both sides is left untouched.
    /// </summary>
    internal static class TagAssignmentReconciler
    {
        public static async Task ReconcileAsync<TJoin>(
            IAsyncEnumerable<int> currentTagIds,
            IAsyncEnumerable<int> desiredTagIds,
            IEntityStore entityStore,
            Func<int, TJoin> toJoinRow,
            CancellationToken cancellationToken) where TJoin : class
        {
            var current = await currentTagIds.ToHashSetAsync(cancellationToken: cancellationToken);
            var desired = await desiredTagIds.ToHashSetAsync(cancellationToken: cancellationToken);

            ChildCollectionReconciler.Reconcile(
                existing: current,
                desired: desired,
                existingKey: id => id,
                desiredKey: id => id,
                delete: tagId => entityStore.Delete(toJoinRow(tagId)),
                insert: tagId => entityStore.Insert(toJoinRow(tagId)));
        }
    }
}
