using Game.Api.Sockets.Commands;
using Xunit;

namespace Game.Api.Tests.Unit
{
    public class ReferenceDataVersioningTests
    {
        private sealed class Sample
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        private static Sample[] SampleSet() =>
        [
            new Sample { Id = 0, Name = "Alpha" },
            new Sample { Id = 1, Name = "Beta" }
        ];

        [Fact]
        public void ComputeVersion_IsDeterministicForIdenticalData()
        {
            var first = ReferenceDataVersioning.ComputeVersion(SampleSet());
            var second = ReferenceDataVersioning.ComputeVersion(SampleSet());

            Assert.Equal(first, second);
        }

        [Fact]
        public void ComputeVersion_ChangesWhenAnyValueChanges()
        {
            var baseline = ReferenceDataVersioning.ComputeVersion(SampleSet());

            var changed = SampleSet();
            changed[1].Name = "Gamma";

            Assert.NotEqual(baseline, ReferenceDataVersioning.ComputeVersion(changed));
        }

        [Fact]
        public void ComputeVersion_ChangesWhenOrderChanges()
        {
            var baseline = ReferenceDataVersioning.ComputeVersion(SampleSet());

            var reordered = SampleSet();
            (reordered[0], reordered[1]) = (reordered[1], reordered[0]);

            Assert.NotEqual(baseline, ReferenceDataVersioning.ComputeVersion(reordered));
        }

        [Fact]
        public void ComputeVersion_ReturnsStableHashForEmptySet()
        {
            var first = ReferenceDataVersioning.ComputeVersion(Array.Empty<Sample>());
            var second = ReferenceDataVersioning.ComputeVersion(Array.Empty<Sample>());

            Assert.Equal(first, second);
            // SHA-256 rendered as uppercase hex is always 64 characters.
            Assert.Equal(64, first.Length);
        }

        [Fact]
        public void GetOrComputeVersion_MatchesDirectComputeVersion()
        {
            var snapshot = SampleSet();

            var memoized = ReferenceDataVersioning.GetOrComputeVersion<Sample>(snapshot, () => snapshot);

            Assert.Equal(ReferenceDataVersioning.ComputeVersion(snapshot), memoized);
        }

        [Fact]
        public void GetOrComputeVersion_ComputesOncePerSnapshotKey()
        {
            var snapshot = SampleSet();
            var computeCount = 0;

            string ComputeFor() => ReferenceDataVersioning.GetOrComputeVersion<Sample>(snapshot, () =>
            {
                computeCount++;
                return snapshot;
            });

            var first = ComputeFor();
            var second = ComputeFor();

            // The hash is serialized once for the snapshot instance, then served from the memo.
            Assert.Equal(1, computeCount);
            Assert.Same(first, second);
        }

        [Fact]
        public void GetOrComputeVersion_RecomputesForNewSnapshotInstance()
        {
            // A cache swap publishes a new snapshot instance; keying on it must recompute even when the
            // underlying values are identical, because the memo is per-instance, not per-value.
            var firstSnapshot = SampleSet();
            var secondSnapshot = SampleSet();
            var computeCount = 0;

            ReferenceDataVersioning.GetOrComputeVersion<Sample>(firstSnapshot, () => { computeCount++; return firstSnapshot; });
            ReferenceDataVersioning.GetOrComputeVersion<Sample>(secondSnapshot, () => { computeCount++; return secondSnapshot; });

            Assert.Equal(2, computeCount);
        }

        [Fact]
        public void GetOrComputeVersion_NewSnapshotReflectsChangedData()
        {
            // The new snapshot's hash must track its (changed) contents, mirroring a real cache swap.
            var firstSnapshot = SampleSet();
            var firstVersion = ReferenceDataVersioning.GetOrComputeVersion<Sample>(firstSnapshot, () => firstSnapshot);

            var secondSnapshot = SampleSet();
            secondSnapshot[1].Name = "Gamma";
            var secondVersion = ReferenceDataVersioning.GetOrComputeVersion<Sample>(secondSnapshot, () => secondSnapshot);

            Assert.NotEqual(firstVersion, secondVersion);
            Assert.Equal(ReferenceDataVersioning.ComputeVersion(secondSnapshot), secondVersion);
        }
    }
}
