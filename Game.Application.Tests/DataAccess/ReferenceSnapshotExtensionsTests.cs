using Game.DataAccess.Repositories;
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
    }
}
