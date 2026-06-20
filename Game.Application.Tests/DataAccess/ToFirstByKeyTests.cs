using Game.DataAccess.PlayerUpdates.Handlers;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Unit tests for the shared <see cref="PlayerUpdateHandlerExtensions.ToFirstByKey"/> dedup helper used by
    /// the batched upsert handlers. It is pure in-process logic, so it is covered here classically rather than
    /// through an integration test.
    /// </summary>
    public class ToFirstByKeyTests
    {
        private sealed record Row(int Key, string Value);

        [Fact]
        public void IndexesEachSourceByItsKey()
        {
            var rows = new[] { new Row(1, "a"), new Row(2, "b") };

            var byKey = rows.ToFirstByKey(r => r.Key);

            Assert.Equal(2, byKey.Count);
            Assert.Equal("a", byKey[1].Value);
            Assert.Equal("b", byKey[2].Value);
        }

        [Fact]
        public void DuplicateKey_KeepsTheFirstRowRatherThanThrowing()
        {
            // A stray duplicate (which the unique key makes impossible in the DB) must not throw the way
            // ToDictionary would — the whole reason the handlers prefer group-by-first.
            var rows = new[] { new Row(1, "first"), new Row(1, "second") };

            var byKey = rows.ToFirstByKey(r => r.Key);

            var row = Assert.Single(byKey);
            Assert.Equal(1, row.Key);
            Assert.Equal("first", row.Value.Value);
        }

        [Fact]
        public void EmptySource_ReturnsEmptyDictionary()
        {
            var byKey = Array.Empty<Row>().ToFirstByKey(r => r.Key);

            Assert.Empty(byKey);
        }

        [Fact]
        public void SupportsCompositeKeys()
        {
            var rows = new[] { new Row(1, "a"), new Row(1, "b") };

            // Keying on the composite (key, value) makes the two rows distinct, mirroring the
            // (StatisticTypeId, EntityId) tuple key in ProgressUpdatedHandler.
            var byKey = rows.ToFirstByKey(r => (r.Key, r.Value));

            Assert.Equal(2, byKey.Count);
            Assert.Equal("a", byKey[(1, "a")].Value);
            Assert.Equal("b", byKey[(1, "b")].Value);
        }
    }
}
