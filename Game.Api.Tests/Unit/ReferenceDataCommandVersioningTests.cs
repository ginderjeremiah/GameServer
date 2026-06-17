using Game.Api.Sockets.Commands;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Pins the memoization wired into <see cref="AbstractReferenceDataCommand{TModel}"/>: the content version
    /// is keyed on the command's <c>VersionKey</c> (the immutable cache snapshot), so it is serialized once per
    /// cache swap rather than on every connect, and a swap (a new snapshot instance) recomputes from the fresh
    /// data. A fake command stands in for the real Get* commands, with the snapshot reference under test control.
    /// </summary>
    public class ReferenceDataCommandVersioningTests
    {
        private sealed record SampleModel(int Id, string Name);

        // A fake reference-data command whose data and version key are driven by a swappable snapshot, mirroring
        // a real Get* command reading its cache holder's Current snapshot.
        private sealed class FakeReferenceDataCommand : AbstractReferenceDataCommand<SampleModel>
        {
            private object _snapshot = new();
            private SampleModel[] _data = [];

            public override string Name { get; set; } = nameof(FakeReferenceDataCommand);

            public int GetReferenceDataCalls { get; private set; }

            // Publishes a new snapshot instance (and its data), exactly as a build-then-swap reload does.
            public void Swap(params SampleModel[] data)
            {
                _snapshot = new object();
                _data = data;
            }

            protected override IEnumerable<SampleModel> GetReferenceData()
            {
                GetReferenceDataCalls++;
                return _data;
            }

            protected override object VersionKey => _snapshot;
        }

        [Fact]
        public void ComputeVersion_SerializesOncePerSnapshot()
        {
            var command = new FakeReferenceDataCommand();
            command.Swap(new SampleModel(0, "Alpha"));

            var first = command.ComputeVersion();
            var second = command.ComputeVersion();

            Assert.Equal(first, second);
            // The data is materialized once for the snapshot, then served from the memo on later connects.
            Assert.Equal(1, command.GetReferenceDataCalls);
        }

        [Fact]
        public void ComputeVersion_RecomputesAfterSwap()
        {
            var command = new FakeReferenceDataCommand();
            command.Swap(new SampleModel(0, "Alpha"));
            var before = command.ComputeVersion();

            command.Swap(new SampleModel(0, "Beta"));
            var after = command.ComputeVersion();

            Assert.NotEqual(before, after);
            // One materialization per snapshot: the original plus the post-swap one.
            Assert.Equal(2, command.GetReferenceDataCalls);
        }

        [Fact]
        public void ComputeVersion_MatchesDirectHashOfTheData()
        {
            var data = new[] { new SampleModel(0, "Alpha"), new SampleModel(1, "Beta") };
            var command = new FakeReferenceDataCommand();
            command.Swap(data);

            // The memoized version must be identical to hashing the same models directly, so the client sees
            // the same version string whether or not the memo was warm.
            Assert.Equal(ReferenceDataVersioning.ComputeVersion(data), command.ComputeVersion());
        }
    }
}
