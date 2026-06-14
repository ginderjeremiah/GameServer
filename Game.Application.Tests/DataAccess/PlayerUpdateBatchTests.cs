using Game.DataAccess;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Unit tests for the scoped <see cref="PlayerUpdateBatch"/> buffer that collects a save's player events so
    /// PlayerRepository.SavePlayer can flush them as one batched LPUSH. It is pure in-process logic with no
    /// out-of-process dependency, so it is covered here classically rather than through an integration test.
    /// </summary>
    public class PlayerUpdateBatchTests
    {
        [Fact]
        public void Drain_ReturnsBufferedEnvelopesInInsertionOrder()
        {
            var batch = new PlayerUpdateBatch();
            var first = new DomainEventEnvelope { Type = "First", Payload = "1" };
            var second = new DomainEventEnvelope { Type = "Second", Payload = "2" };

            batch.Add(first);
            batch.Add(second);

            Assert.Equal(new[] { first, second }, batch.Drain());
        }

        [Fact]
        public void Drain_ClearsTheBuffer_SoASecondDrainIsEmpty()
        {
            var batch = new PlayerUpdateBatch();
            batch.Add(new DomainEventEnvelope { Type = "Only", Payload = "x" });

            Assert.Single(batch.Drain());
            // A second save in the same scope must not re-publish the first save's events.
            Assert.Empty(batch.Drain());
        }

        [Fact]
        public void Drain_WithNothingBuffered_ReturnsEmpty()
        {
            var batch = new PlayerUpdateBatch();

            Assert.Empty(batch.Drain());
        }
    }
}
