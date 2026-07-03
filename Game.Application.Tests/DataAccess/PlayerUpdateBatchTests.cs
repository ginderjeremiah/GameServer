using Game.Abstractions.Infrastructure;
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

        [Fact]
        public void PlayerSaveInProgress_IsFalseUntilBegun_AndResetOnScopeDisposal()
        {
            var batch = new PlayerUpdateBatch();
            Assert.False(batch.PlayerSaveInProgress);

            // A progress save raised within this window joins the player save's flush rather than publishing
            // on its own; disposing the scope (SavePlayer's dispatch having settled) ends the window.
            using (batch.BeginPlayerSave())
            {
                Assert.True(batch.PlayerSaveInProgress);
            }

            Assert.False(batch.PlayerSaveInProgress);
        }

        [Fact]
        public void RunFlushedCallbacks_RunsRegisteredActionsInOrder_ThenClearsThem()
        {
            var batch = new PlayerUpdateBatch();
            var ran = new List<string>();
            batch.OnFlushed(() => ran.Add("first"));
            batch.OnFlushed(() => ran.Add("second"));

            batch.RunFlushedCallbacks();
            Assert.Equal(["first", "second"], ran);

            // A second flush in the same scope must not re-run the first flush's deferred cache advances.
            batch.RunFlushedCallbacks();
            Assert.Equal(["first", "second"], ran);
        }

        [Fact]
        public void RunFlushedCallbacks_WithNoneRegistered_IsNoOp()
        {
            var batch = new PlayerUpdateBatch();

            // No deferred callbacks (e.g. a plain player save with no nested progress save) — must not throw.
            batch.RunFlushedCallbacks();
        }

        [Fact]
        public async Task FlushAsync_PublishSucceeds_ClearsTheBufferAndRunsFlushedCallbacksOnlyOnce()
        {
            var batch = new PlayerUpdateBatch();
            batch.Add(new DomainEventEnvelope { Type = "Only", Payload = "x" });
            var flushCount = 0;
            batch.OnFlushed(() => flushCount++);
            var pubsub = new RecordingPubSubService();

            await batch.FlushAsync(pubsub);

            Assert.Single(pubsub.Calls);
            Assert.Single(pubsub.Calls[0]);
            Assert.Equal(1, flushCount);

            // A second flush in the same scope must not re-publish the first flush's event or re-run its callback.
            await batch.FlushAsync(pubsub);
            Assert.Equal(2, pubsub.Calls.Count);
            Assert.Empty(pubsub.Calls[1]);
            Assert.Equal(1, flushCount);
        }

        [Fact]
        public async Task FlushAsync_PublishThrows_PreservesTheBufferedEventAndCallbackForTheNextFlush()
        {
            var batch = new PlayerUpdateBatch();
            var envelope = new DomainEventEnvelope { Type = "Only", Payload = "x" };
            batch.Add(envelope);
            var flushCount = 0;
            batch.OnFlushed(() => flushCount++);

            // A publish failure (a transient Redis blip/timeout) must neither drain the buffered event nor run
            // (or clear) the deferred callback — both must survive for the batch's next flush attempt (#1494).
            await Assert.ThrowsAsync<InvalidOperationException>(() => batch.FlushAsync(new ThrowingPubSubService()));
            Assert.Equal(0, flushCount);

            var pubsub = new RecordingPubSubService();
            await batch.FlushAsync(pubsub);

            Assert.Single(pubsub.Calls);
            Assert.Equal([envelope], pubsub.Calls[0]);
            Assert.Equal(1, flushCount);
        }

        // Records each PublishBatch call's items rather than verifying call behavior via a mocking framework,
        // matching this project's classical (state-based) testing style.
        private sealed class RecordingPubSubService : IPubSubService
        {
            public List<List<object?>> Calls { get; } = [];

            public Task PublishBatch<T>(string channel, string queueName, IEnumerable<T> queueData, CancellationToken cancellationToken = default)
            {
                Calls.Add(queueData.Select(data => (object?)data).ToList());
                return Task.CompletedTask;
            }

            public Task Publish(string channel, string message, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Publish(string channel, string queueName, string queueData, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Publish<T>(string channel, string queueName, T queueData, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Wake(string channel) => throw new NotSupportedException();
            public Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null) => throw new NotSupportedException();
            public Task Subscribe(string channel, string queueName, Action<(IPubSubQueue queue, string channel)> action, string id) => throw new NotSupportedException();
            public Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string id) => throw new NotSupportedException();
            public Task UnSubscribe(string channel) => throw new NotSupportedException();
            public Task UnSubscribe(string channel, string id) => throw new NotSupportedException();
            public IPubSubQueue GetQueue(string queueName) => throw new NotSupportedException();
        }

        private sealed class ThrowingPubSubService : IPubSubService
        {
            public Task PublishBatch<T>(string channel, string queueName, IEnumerable<T> queueData, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("Simulated transient publish failure.");

            public Task Publish(string channel, string message, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Publish(string channel, string queueName, string queueData, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Publish<T>(string channel, string queueName, T queueData, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task Wake(string channel) => throw new NotSupportedException();
            public Task Subscribe(string channel, Action<(string message, string channel)> action, string? id = null) => throw new NotSupportedException();
            public Task Subscribe(string channel, string queueName, Action<(IPubSubQueue queue, string channel)> action, string id) => throw new NotSupportedException();
            public Task Subscribe(string channel, string queueName, Func<(IPubSubQueue queue, string channel), Task> action, string id) => throw new NotSupportedException();
            public Task UnSubscribe(string channel) => throw new NotSupportedException();
            public Task UnSubscribe(string channel, string id) => throw new NotSupportedException();
            public IPubSubQueue GetQueue(string queueName) => throw new NotSupportedException();
        }
    }
}
