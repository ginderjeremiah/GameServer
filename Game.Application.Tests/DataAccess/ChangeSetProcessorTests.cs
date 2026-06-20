using Game.Abstractions;
using Game.Abstractions.Contracts.Admin;
using Game.DataAccess.Repositories.Admin;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    public class ChangeSetProcessorTests
    {
        private sealed class TestModel : IModel
        {
            public required string Tag { get; init; }
        }

        private static Change<TestModel> Change(EChangeType changeType, string tag) => new()
        {
            ChangeType = changeType,
            Item = new TestModel { Tag = tag },
        };

        [Fact]
        public void Apply_DispatchesEachChangeToHandlerMatchingItsType()
        {
            var added = new List<string>();
            var edited = new List<string>();
            var deleted = new List<string>();

            var changes = new[]
            {
                Change(EChangeType.Add, "a"),
                Change(EChangeType.Edit, "e"),
                Change(EChangeType.Delete, "d"),
            };

            ChangeSetProcessor.Apply(changes,
                add: item => added.Add(item.Tag),
                edit: item => edited.Add(item.Tag),
                delete: item => deleted.Add(item.Tag));

            Assert.Equal(["a"], added);
            Assert.Equal(["e"], edited);
            Assert.Equal(["d"], deleted);
        }

        [Fact]
        public void Apply_ProcessesChangesInDescendingChangeTypeOrder()
        {
            // Deletes (2) must be flushed before edits (1) and adds (0) so that a delete and a
            // re-add of the same key in one batch settle in the right order. The input is
            // deliberately scrambled to prove the processor — not the caller — owns the ordering.
            var order = new List<EChangeType>();

            var changes = new[]
            {
                Change(EChangeType.Add, "a"),
                Change(EChangeType.Delete, "d"),
                Change(EChangeType.Edit, "e"),
            };

            ChangeSetProcessor.Apply(changes,
                add: _ => order.Add(EChangeType.Add),
                edit: _ => order.Add(EChangeType.Edit),
                delete: _ => order.Add(EChangeType.Delete));

            Assert.Equal([EChangeType.Delete, EChangeType.Edit, EChangeType.Add], order);
        }

        [Fact]
        public void Apply_PreservesRelativeOrderWithinAChangeType()
        {
            var added = new List<string>();

            var changes = new[]
            {
                Change(EChangeType.Add, "first"),
                Change(EChangeType.Add, "second"),
                Change(EChangeType.Add, "third"),
            };

            ChangeSetProcessor.Apply(changes,
                add: item => added.Add(item.Tag),
                edit: _ => { },
                delete: _ => { });

            Assert.Equal(["first", "second", "third"], added);
        }

        [Fact]
        public void Apply_AddAndEdit_WithNoDeleteHandler_Succeeds()
        {
            // Reference repos omit the delete handler (their records are retired, not deleted), but
            // add/edit must still flow through.
            var added = new List<string>();
            var edited = new List<string>();

            var changes = new[]
            {
                Change(EChangeType.Add, "a"),
                Change(EChangeType.Edit, "e"),
            };

            var result = ChangeSetProcessor.Apply(changes,
                add: item => added.Add(item.Tag),
                edit: item => edited.Add(item.Tag));

            Assert.True(result.Succeeded);
            Assert.Equal(["a"], added);
            Assert.Equal(["e"], edited);
        }

        [Fact]
        public void Apply_DeleteChange_WithNoDeleteHandler_ReturnsBusinessFailureAndAppliesNothing()
        {
            // A top-level Delete against a retire-only reference set is a client input error, not a server
            // fault: it is rejected as a graceful business failure (which the API surfaces as a 400) rather
            // than thrown, because opening an id gap would silently mis-resolve index-based lookups. The
            // sibling Add/Edit in the same batch must not be applied — the rejection keeps it atomic.
            var added = new List<string>();
            var edited = new List<string>();

            var changes = new[]
            {
                Change(EChangeType.Add, "a"),
                Change(EChangeType.Edit, "e"),
                Change(EChangeType.Delete, "d"),
            };

            var result = ChangeSetProcessor.Apply(changes,
                add: item => added.Add(item.Tag),
                edit: item => edited.Add(item.Tag));

            Assert.False(result.Succeeded);
            Assert.Equal(
                "Delete is not supported for TestModel: reference records are retired, not deleted.",
                result.ErrorMessage);
            Assert.Empty(added);
            Assert.Empty(edited);
        }

        [Fact]
        public void Apply_EmptyChangeSet_InvokesNoHandlers()
        {
            var invoked = false;

            ChangeSetProcessor.Apply(Array.Empty<Change<TestModel>>(),
                add: _ => invoked = true,
                edit: _ => invoked = true,
                delete: _ => invoked = true);

            Assert.False(invoked);
        }

        [Fact]
        public void Apply_DuplicateEditKey_RejectsGracefullyWithoutApplying()
        {
            // Two Edits of the same key map to two Update ops on distinct instances sharing that key, which
            // EF double-tracks and rejects mid-batch. The malformed batch is rejected up front as a graceful
            // business failure before anything is applied.
            var edited = new List<string>();

            var result = ChangeSetProcessor.Apply(
            [
                Change(EChangeType.Edit, "k"),
                Change(EChangeType.Edit, "k"),
            ],
                add: _ => { },
                edit: item => edited.Add(item.Tag),
                key: item => item.Tag,
                resourceName: "widget");

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted widget change set contains duplicate entries.", result.ErrorMessage);
            Assert.Empty(edited);
        }

        [Fact]
        public void Apply_DuplicateDeleteKey_RejectsGracefullyWithoutApplying()
        {
            var deleted = new List<string>();

            var result = ChangeSetProcessor.Apply(
            [
                Change(EChangeType.Delete, "k"),
                Change(EChangeType.Delete, "k"),
            ],
                add: _ => { },
                edit: _ => { },
                delete: item => deleted.Add(item.Tag),
                key: item => item.Tag,
                resourceName: "widget");

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted widget change set contains duplicate entries.", result.ErrorMessage);
            Assert.Empty(deleted);
        }

        [Fact]
        public void Apply_SameKeyAcrossEditAndDelete_RejectsGracefullyWithoutApplying()
        {
            var edited = new List<string>();
            var deleted = new List<string>();

            var result = ChangeSetProcessor.Apply(
            [
                Change(EChangeType.Edit, "k"),
                Change(EChangeType.Delete, "k"),
            ],
                add: _ => { },
                edit: item => edited.Add(item.Tag),
                delete: item => deleted.Add(item.Tag),
                key: item => item.Tag,
                resourceName: "widget");

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted widget change set contains duplicate entries.", result.ErrorMessage);
            Assert.Empty(edited);
            Assert.Empty(deleted);
        }

        [Fact]
        public void Apply_DuplicateAddKey_IsNotRejected()
        {
            // Adds carry a store-generated sentinel key (every new row's id resolves on commit), so two Adds
            // never collide in EF — the guard deliberately excludes them or it would falsely reject distinct
            // new records that share the wire sentinel.
            var added = new List<string>();

            var result = ChangeSetProcessor.Apply(
            [
                Change(EChangeType.Add, "k"),
                Change(EChangeType.Add, "k"),
            ],
                add: item => added.Add(item.Tag),
                edit: _ => { },
                key: item => item.Tag,
                resourceName: "widget");

            Assert.True(result.Succeeded);
            Assert.Equal(["k", "k"], added);
        }

        [Fact]
        public void Apply_DistinctEditKeys_Succeeds()
        {
            var edited = new List<string>();

            var result = ChangeSetProcessor.Apply(
            [
                Change(EChangeType.Edit, "a"),
                Change(EChangeType.Edit, "b"),
            ],
                add: _ => { },
                edit: item => edited.Add(item.Tag),
                key: item => item.Tag,
                resourceName: "widget");

            Assert.True(result.Succeeded);
            Assert.Equal(["a", "b"], edited);
        }

        [Fact]
        public void Apply_NoKeySelector_DoesNotDedup()
        {
            // With no key selector supplied the guard is off and every change applies as-is.
            var edited = new List<string>();

            var result = ChangeSetProcessor.Apply(
            [
                Change(EChangeType.Edit, "k"),
                Change(EChangeType.Edit, "k"),
            ],
                add: _ => { },
                edit: item => edited.Add(item.Tag));

            Assert.True(result.Succeeded);
            Assert.Equal(["k", "k"], edited);
        }
    }
}
