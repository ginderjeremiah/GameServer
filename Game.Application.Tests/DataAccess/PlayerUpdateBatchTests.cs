using Game.DataAccess;
using Game.TestInfrastructure.Helpers;
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
        public void PlayerSaveInProgress_StaysTrueAcrossNestedScope_UntilTheOutermostDisposes()
        {
            var batch = new PlayerUpdateBatch();

            // Mirrors OfflineProgressService: an outer BeginBatch scope wraps a nested SavePlayer, whose own
            // internal BeginPlayerSave must not end the outer caller's window when it disposes first (#2001).
            using (batch.BeginPlayerSave())
            {
                Assert.True(batch.PlayerSaveInProgress);

                using (batch.BeginPlayerSave())
                {
                    Assert.True(batch.PlayerSaveInProgress);
                }

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
        public async Task FlushAsync_PublishSucceeds_PublishesInInsertionOrder_ThenClearsTheBufferAndRunsFlushedCallbacksOnlyOnce()
        {
            var batch = new PlayerUpdateBatch();
            var first = new DomainEventEnvelope { Type = "First", Payload = "1" };
            var second = new DomainEventEnvelope { Type = "Second", Payload = "2" };
            batch.Add(first);
            batch.Add(second);
            var flushCount = 0;
            batch.OnFlushed(() => flushCount++);
            var pubsub = new RecordingPubSubService();

            await batch.FlushAsync(pubsub);

            Assert.Equal([first, second], pubsub.Calls[0]);
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
        private sealed class RecordingPubSubService : NotSupportedPubSubService
        {
            public List<List<object?>> Calls { get; } = [];

            public override Task PublishBatch<T>(string channel, string queueName, IEnumerable<T> queueData, CancellationToken cancellationToken = default)
            {
                Calls.Add(queueData.Select(data => (object?)data).ToList());
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingPubSubService : NotSupportedPubSubService
        {
            public override Task PublishBatch<T>(string channel, string queueName, IEnumerable<T> queueData, CancellationToken cancellationToken = default)
                => throw new InvalidOperationException("Simulated transient publish failure.");
        }
    }
}
