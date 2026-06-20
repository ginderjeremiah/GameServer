using Game.DataAccess.Repositories;
using Game.Infrastructure.Entities;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Pins the zero-based-id reference snapshot index helpers (#487): <c>Lookup</c> returns null for an
    /// out-of-range id, while <c>GetById</c> throws a descriptive <see cref="ArgumentOutOfRangeException"/>
    /// naming the id and set. Pure logic with no out-of-process dependency, so covered by classical unit tests
    /// rather than the integration suite.
    /// </summary>
    public class ReferenceSnapshotExtensionsTests
    {
        private static readonly IReadOnlyList<string> Snapshot = ["a", "b", "c"];

        [Theory]
        [InlineData(0, "a")]
        [InlineData(1, "b")]
        [InlineData(2, "c")]
        public void Lookup_IdInRange_ReturnsRecord(int id, string expected)
        {
            Assert.Equal(expected, Snapshot.Lookup(id));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(3)]
        [InlineData(int.MaxValue)]
        public void Lookup_IdOutOfRange_ReturnsNull(int id)
        {
            Assert.Null(Snapshot.Lookup(id));
        }

        [Fact]
        public void Lookup_EmptySnapshot_ReturnsNull()
        {
            IReadOnlyList<string> empty = [];

            Assert.Null(empty.Lookup(0));
        }

        [Theory]
        [InlineData(0, "a")]
        [InlineData(2, "c")]
        public void GetById_IdInRange_ReturnsRecord(int id, string expected)
        {
            Assert.Equal(expected, Snapshot.GetById(id, "thing"));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(3)]
        [InlineData(int.MaxValue)]
        public void GetById_IdOutOfRange_ThrowsDescriptiveArgumentOutOfRange(int id)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Snapshot.GetById(id, "widget"));

            Assert.Equal(id, ex.ActualValue);
            Assert.Contains("widget", ex.Message);
            Assert.Contains(id.ToString(), ex.Message);
        }

        [Fact]
        public void GetById_EmptySnapshot_Throws()
        {
            IReadOnlyList<string> empty = [];

            Assert.Throws<ArgumentOutOfRangeException>(() => empty.GetById(0, "thing"));
        }

        [Fact]
        public void AssertZeroBasedContiguity_ContiguousIds_DoesNotThrow()
        {
            IReadOnlyList<Record> contiguous = [new(0), new(1), new(2)];

            contiguous.AssertZeroBasedContiguity(r => r.Id, "things");
        }

        [Fact]
        public void AssertZeroBasedContiguity_EmptySnapshot_DoesNotThrow()
        {
            IReadOnlyList<Record> empty = [];

            empty.AssertZeroBasedContiguity(r => r.Id, "things");
        }

        [Fact]
        public void AssertZeroBasedContiguity_DoesNotStartAtZero_Throws()
        {
            // Sorted by id but seeded from 1 — a gap at index 0 that OrderBy can't catch.
            IReadOnlyList<Record> offByOne = [new(1), new(2), new(3)];

            var ex = Assert.Throws<InvalidOperationException>(() => offByOne.AssertZeroBasedContiguity(r => r.Id, "widgets"));
            Assert.Contains("widgets", ex.Message);
            Assert.Contains("index 0", ex.Message);
            Assert.Contains("Id 1", ex.Message);
        }

        [Fact]
        public void AssertZeroBasedContiguity_GapInMiddle_ThrowsAtFirstMismatch()
        {
            // Sorted, contiguous through index 1, then missing id 2 — the record at index 2 has Id 3.
            IReadOnlyList<Record> gap = [new(0), new(1), new(3), new(4)];

            var ex = Assert.Throws<InvalidOperationException>(() => gap.AssertZeroBasedContiguity(r => r.Id, "widgets"));
            Assert.Contains("index 2", ex.Message);
            Assert.Contains("Id 3", ex.Message);
        }

        [Fact]
        public void AssertZeroBasedContiguity_EntityOverload_ReadsIdOffTheContract()
        {
            // The entity overload reads Id off IZeroBasedIdentityEntity; a covariant entity list is accepted.
            IReadOnlyList<FakeEntity> contiguous = [new() { Id = 0 }, new() { Id = 1 }];
            contiguous.AssertZeroBasedContiguity("fakes");

            IReadOnlyList<FakeEntity> gap = [new() { Id = 0 }, new() { Id = 2 }];
            var ex = Assert.Throws<InvalidOperationException>(() => gap.AssertZeroBasedContiguity("fakes"));
            Assert.Contains("index 1", ex.Message);
        }

        private sealed record Record(int Id);

        private sealed class FakeEntity : IZeroBasedIdentityEntity
        {
            public int Id { get; set; }
        }
    }
}
