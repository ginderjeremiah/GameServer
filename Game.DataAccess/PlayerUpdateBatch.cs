using Game.Abstractions.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Game.DataAccess
{
    /// <summary>
    /// Scoped buffer that collects the player persistence events raised during a single
    /// <see cref="Repositories.PlayerRepository.SavePlayer"/> so they can be flushed to the write-behind
    /// queue as one batched LPUSH instead of one round-trip per event. <see cref="PlayerPersistencePublisher"/>
    /// fills it as each event is dispatched; <c>SavePlayer</c> flushes and publishes it after the dispatch
    /// settles via <see cref="FlushAsync"/>. It is registered scoped so the publisher (constructed per dispatch)
    /// and the repository share the same instance within a request scope, and a successful flush clears it so
    /// a second save in the same scope starts fresh.
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
        private readonly ILogger<PlayerUpdateBatch> _logger;

        private int _playerSaveDepth;

        public PlayerUpdateBatch(ILogger<PlayerUpdateBatch> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// True while a <see cref="Repositories.PlayerRepository.SavePlayer"/> is mid-flight — between
        /// <see cref="BeginPlayerSave"/> and the returned scope's disposal. A progress save observing this
        /// joins the in-flight player save's single flush rather than publishing on its own. Tracked as a
        /// depth counter rather than a flag so a nested <see cref="BeginPlayerSave"/> — e.g.
        /// <c>SavePlayer</c>'s own internal scope opened while an outer caller already holds one via
        /// <see cref="Repositories.PlayerRepository.BeginBatch"/> — doesn't end the outer scope's window when
        /// the inner one disposes first.
        /// </summary>
        public bool PlayerSaveInProgress => _playerSaveDepth > 0;

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
        /// during its event dispatch joins this batch instead of publishing independently. Safe to nest — the
        /// window stays open until every <see cref="BeginPlayerSave"/> call has had its scope disposed.
        /// </summary>
        public IDisposable BeginPlayerSave()
        {
            _playerSaveDepth++;
            return new PlayerSaveScope(this);
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
        /// enqueued (publish-before-cache). Each callback is isolated (logged, not rethrown): the cache advance
        /// is best-effort by design, and by this point the flush it rides has already durably succeeded, so a
        /// callback fault (e.g. a dropped Redis connection) must never be mistaken for a flush failure and skip
        /// the player cache-blob write that follows in <see cref="Repositories.PlayerRepository.SavePlayer"/> (#2271).
        /// </summary>
        public void RunFlushedCallbacks()
        {
            if (_onFlushed.Count == 0)
            {
                return;
            }

            foreach (var action in _onFlushed)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "A deferred OnFlushed callback threw after its batch's flush already succeeded; continuing with the remaining callbacks.");
                }
            }

            _onFlushed.Clear();
        }

        private sealed class PlayerSaveScope(PlayerUpdateBatch batch) : IDisposable
        {
            public void Dispose() => batch._playerSaveDepth--;
        }
    }
}
