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
    }
}
