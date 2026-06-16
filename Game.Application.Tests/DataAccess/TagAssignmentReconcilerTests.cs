using Game.DataAccess.Repositories.Admin;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    public class TagAssignmentReconcilerTests
    {
        // Stands in for the two real join rows (ItemTag / ItemModTag), which differ only in their owner FK.
        private sealed record TestJoinRow(int TagId);

        private static async IAsyncEnumerable<int> AsAsync(params int[] ids)
        {
            foreach (var id in ids)
            {
                yield return id;
            }

            await Task.CompletedTask;
        }

        private static Task Reconcile(int[] current, int[] desired, RecordingEntityStore store) =>
            TagAssignmentReconciler.ReconcileAsync(
                AsAsync(current),
                AsAsync(desired),
                store,
                tagId => new TestJoinRow(tagId));

        private static List<int> TagIds(IEnumerable<object> rows) =>
            rows.Cast<TestJoinRow>().Select(r => r.TagId).OrderBy(id => id).ToList();

        [Fact]
        public async Task ReconcileAsync_DeletesCurrentNotDesired_InsertsDesiredNotCurrent_LeavesCommon()
        {
            var store = new RecordingEntityStore();

            // Current: 1, 2. Desired: 2, 3. So 1 is deleted, 3 inserted, 2 left untouched.
            await Reconcile(current: [1, 2], desired: [2, 3], store);

            Assert.Equal([1], TagIds(store.Deleted));
            Assert.Equal([3], TagIds(store.Inserted));
        }

        [Fact]
        public async Task ReconcileAsync_EmptyDesired_DeletesAllCurrent()
        {
            var store = new RecordingEntityStore();

            await Reconcile(current: [1, 2], desired: [], store);

            Assert.Equal([1, 2], TagIds(store.Deleted));
            Assert.Empty(store.Inserted);
        }

        [Fact]
        public async Task ReconcileAsync_EmptyCurrent_InsertsAllDesired()
        {
            var store = new RecordingEntityStore();

            await Reconcile(current: [], desired: [1, 2], store);

            Assert.Equal([1, 2], TagIds(store.Inserted));
            Assert.Empty(store.Deleted);
        }

        [Fact]
        public async Task ReconcileAsync_IdenticalSets_InsertsAndDeletesNothing()
        {
            var store = new RecordingEntityStore();

            await Reconcile(current: [1, 2], desired: [1, 2], store);

            Assert.Empty(store.Inserted);
            Assert.Empty(store.Deleted);
        }
    }
}
