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
        public void Apply_EmptyChangeSet_InvokesNoHandlers()
        {
            var invoked = false;

            ChangeSetProcessor.Apply(Array.Empty<Change<TestModel>>(),
                add: _ => invoked = true,
                edit: _ => invoked = true,
                delete: _ => invoked = true);

            Assert.False(invoked);
        }
    }
}
