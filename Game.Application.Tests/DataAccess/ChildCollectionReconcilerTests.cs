using Game.Abstractions.DataAccess.Admin;
using Game.DataAccess.Repositories.Admin;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    public class ChildCollectionReconcilerTests
    {
        // The two sides deliberately use different CLR types (and key types) to prove the helper matches
        // them through the two selectors — mirroring an entity's int id vs. a contract's enum key.
        private sealed record ExistingChild(int Key);

        private sealed record DesiredChild(string Key, string Payload);

        private sealed class Recorder
        {
            public List<int> Deleted { get; } = [];
            public List<string> Inserted { get; } = [];
            public List<string> Updated { get; } = [];
        }

        private static AdminSaveResult Reconcile(
            IReadOnlyCollection<ExistingChild> existing,
            IReadOnlyCollection<DesiredChild> desired,
            Recorder recorder,
            bool withUpdate = true)
        {
            return ChildCollectionReconciler.Reconcile(
                existing: existing,
                desired: desired,
                existingKey: e => e.Key,
                desiredKey: d => int.Parse(d.Key),
                delete: e => recorder.Deleted.Add(e.Key),
                insert: d => recorder.Inserted.Add(d.Payload),
                resourceName: "child",
                update: withUpdate ? d => recorder.Updated.Add(d.Payload) : null);
        }

        [Fact]
        public void Reconcile_DeletesExistingNotInDesired_UpdatesPresentInBoth_InsertsNew()
        {
            var recorder = new Recorder();

            // Existing keys: 1, 2. Desired keys: 2 (update), 3 (insert). So 1 is deleted.
            var existing = new[] { new ExistingChild(1), new ExistingChild(2) };
            var desired = new[] { new DesiredChild("2", "two"), new DesiredChild("3", "three") };

            var result = Reconcile(existing, desired, recorder);

            Assert.True(result.Succeeded);
            Assert.Equal([1], recorder.Deleted);
            Assert.Equal(["two"], recorder.Updated);
            Assert.Equal(["three"], recorder.Inserted);
        }

        [Fact]
        public void Reconcile_DuplicateDesiredKeys_ReturnsFailureAndInvokesNoHandlers()
        {
            // Two desired entries sharing a key would both miss the existing-membership check and
            // double-insert (a unique violation at commit), so the malformed set is rejected up front and
            // nothing is staged.
            var recorder = new Recorder();

            var existing = new[] { new ExistingChild(1) };
            var desired = new[] { new DesiredChild("2", "two"), new DesiredChild("2", "two-again") };

            var result = Reconcile(existing, desired, recorder);

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted child set contains duplicate entries.", result.ErrorMessage);
            Assert.Empty(recorder.Deleted);
            Assert.Empty(recorder.Updated);
            Assert.Empty(recorder.Inserted);
        }

        [Fact]
        public void Reconcile_EmptyDesired_DeletesAllExisting()
        {
            var recorder = new Recorder();

            var existing = new[] { new ExistingChild(1), new ExistingChild(2) };

            Reconcile(existing, [], recorder);

            Assert.Equal([1, 2], recorder.Deleted);
            Assert.Empty(recorder.Updated);
            Assert.Empty(recorder.Inserted);
        }

        [Fact]
        public void Reconcile_EmptyExisting_InsertsAllDesired()
        {
            var recorder = new Recorder();

            var desired = new[] { new DesiredChild("1", "one"), new DesiredChild("2", "two") };

            Reconcile([], desired, recorder);

            Assert.Empty(recorder.Deleted);
            Assert.Empty(recorder.Updated);
            Assert.Equal(["one", "two"], recorder.Inserted);
        }

        [Fact]
        public void Reconcile_IdenticalKeySets_UpdatesEachAndDeletesOrInsertsNothing()
        {
            var recorder = new Recorder();

            var existing = new[] { new ExistingChild(1), new ExistingChild(2) };
            var desired = new[] { new DesiredChild("1", "one"), new DesiredChild("2", "two") };

            Reconcile(existing, desired, recorder);

            Assert.Empty(recorder.Deleted);
            Assert.Empty(recorder.Inserted);
            Assert.Equal(["one", "two"], recorder.Updated);
        }

        [Fact]
        public void Reconcile_BothEmpty_InvokesNoHandlers()
        {
            var recorder = new Recorder();

            Reconcile([], [], recorder);

            Assert.Empty(recorder.Deleted);
            Assert.Empty(recorder.Updated);
            Assert.Empty(recorder.Inserted);
        }

        [Fact]
        public void Reconcile_NoUpdateHandler_PresentInBothAreLeftUntouched()
        {
            // A pure join row (e.g. EnemySkill) has no payload to update, so a key present on both sides
            // is a no-op — only the genuinely-removed and genuinely-new keys produce work.
            var recorder = new Recorder();

            var existing = new[] { new ExistingChild(1), new ExistingChild(2) };
            var desired = new[] { new DesiredChild("2", "two"), new DesiredChild("3", "three") };

            Reconcile(existing, desired, recorder, withUpdate: false);

            Assert.Equal([1], recorder.Deleted);
            Assert.Equal(["three"], recorder.Inserted);
            Assert.Empty(recorder.Updated);
        }

        [Fact]
        public void Reconcile_DeletePassesExistingEntity_InsertAndUpdatePassDesiredItem()
        {
            // The delete handler must receive the existing entity (to read its key for a fresh, navigation-free
            // delete), while insert/update receive the desired contract (carrying the new payload).
            ExistingChild? deletedChild = null;
            DesiredChild? insertedItem = null;
            DesiredChild? updatedItem = null;

            var existing = new[] { new ExistingChild(1), new ExistingChild(2) };
            var desired = new[] { new DesiredChild("2", "two"), new DesiredChild("3", "three") };

            ChildCollectionReconciler.Reconcile(
                existing: existing,
                desired: desired,
                existingKey: e => e.Key,
                desiredKey: d => int.Parse(d.Key),
                delete: e => deletedChild = e,
                insert: d => insertedItem = d,
                resourceName: "child",
                update: d => updatedItem = d);

            Assert.Equal(new ExistingChild(1), deletedChild);
            Assert.Equal(new DesiredChild("3", "three"), insertedItem);
            Assert.Equal(new DesiredChild("2", "two"), updatedItem);
        }
    }
}
