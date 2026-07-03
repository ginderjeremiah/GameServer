using Game.Abstractions.Infrastructure;

namespace Game.DataAccess
{
    /// <summary>
    /// Scoped buffer that collects the player persistence events raised during a single
    /// <see cref="Repositories.PlayerRepository.SavePlayer"/> so they can be flushed to the write-behind
    /// queue as one batched LPUSH instead of one round-trip per event. <see cref="PlayerPersistencePublisher"/>
    /// fills it as each event is dispatched; <c>SavePlayer</c> drains and publishes it after the dispatch
    /// settles. It is registered scoped so the publisher (constructed per dispatch) and the repository share
    /// the same instance within a request scope, and <see cref="Drain"/> clears it so a second save in the
    /// same scope starts fresh.
    /// <para>
    /// A progress save raised <em>within</em> a player save — the live battle-completion path, where
    /// <c>SavePlayer</c>'s event dispatch reaches <c>BattleStatisticsEventHandler</c>, which saves progress —
    /// joins this same batch instead of issuing its own queue round-trip, so the player and progress writes
    /// of a battle tick collapse onto a single flush (#1237). It buffers its event here and defers its cache
    /// advance via <see cref="OnFlushed"/> so the advance lands only after <c>SavePlayer</c> enqueues the
    /// event, preserving the publish-before-cache ordering across the shared batch. A progress save outside a
    /// player save (<see cref="PlayerSaveInProgress"/> is <c>false</c>) flushes itself, so it is never stranded.
    /// </para>
    /// </summary>
    internal sealed class PlayerUpdateBatch
    {
        private readonly List<DomainEventEnvelope> _events = [];
        private readonly List<Action> _onFlushed = [];

        /// <summary>
        /// True while a <see cref="Repositories.PlayerRepository.SavePlayer"/> is mid-flight — between
        /// <see cref="BeginPlayerSave"/> and the returned scope's disposal. A progress save observing this
        /// joins the in-flight player save's single flush rather than publishing on its own.
        /// </summary>
        public bool PlayerSaveInProgress { get; private set; }

        public void Add(DomainEventEnvelope envelope)
        {
            _events.Add(envelope);
        }

        /// <summary>
        /// Registers an action to run once the batch has been flushed to the queue (see
        /// <see cref="RunFlushedCallbacks"/>). A batched progress save uses this to defer its cache advance
        /// until the flush has enqueued the event, so a failed enqueue can't leave the cache holding a
        /// snapshot that was never queued.
        /// </summary>
        public void OnFlushed(Action action)
        {
            _onFlushed.Add(action);
        }

        /// <summary>
        /// Marks a player save as in progress until the returned scope is disposed, so a progress save raised
        /// during its event dispatch joins this batch instead of publishing independently.
        /// </summary>
        public IDisposable BeginPlayerSave()
        {
            PlayerSaveInProgress = true;
            return new PlayerSaveScope(this);
        }

        /// <summary>
        /// Returns the buffered events and clears the buffer so it is ready for the next save.
        /// </summary>
        public IReadOnlyList<DomainEventEnvelope> Drain()
        {
            if (_events.Count == 0)
            {
                return [];
            }

            var drained = _events.ToArray();
            _events.Clear();
            return drained;
        }

        /// <summary>
        /// Publishes the buffered batch to the write-behind queue and only then clears it and runs any
        /// deferred <see cref="OnFlushed"/> callbacks. If the publish throws (a transient Redis blip/timeout),
        /// the buffered envelopes and callbacks are left exactly as they were — nothing is drained or run —
        /// so they are carried into the batch's next flush attempt instead of being silently discarded (#1494).
        /// Passing the live buffer directly (rather than draining a snapshot first) is safe because
        /// <see cref="Game.Abstractions.Infrastructure.IPubSubService.PublishBatch{T}"/> materializes it
        /// synchronously before its first await.
        /// </summary>
        public async Task FlushAsync(IPubSubService pubsub, CancellationToken cancellationToken = default)
        {
            await pubsub.PublishBatch(Constants.PUBSUB_PLAYER_CHANNEL, Constants.PUBSUB_PLAYER_QUEUE, _events, cancellationToken);
            _events.Clear();
            RunFlushedCallbacks();
        }

        /// <summary>
        /// Runs and clears the actions registered via <see cref="OnFlushed"/>. <see cref="FlushAsync"/> calls
        /// this after its flush so a batched progress save's deferred cache advance lands only once its event is
        /// enqueued (publish-before-cache).
        /// </summary>
        public void RunFlushedCallbacks()
        {
            if (_onFlushed.Count == 0)
            {
                return;
            }

            foreach (var action in _onFlushed)
            {
                action();
            }

            _onFlushed.Clear();
        }

        private sealed class PlayerSaveScope(PlayerUpdateBatch batch) : IDisposable
        {
            public void Dispose() => batch.PlayerSaveInProgress = false;
        }
    }
}
