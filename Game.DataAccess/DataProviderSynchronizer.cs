using Game.Abstractions.Infrastructure;
using Game.Core;
using Game.DataAccess.PlayerUpdates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Game.DataAccess
{
    internal class DataProviderSynchronizer : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly IPubSubService _pubsub;
        private readonly ILogger<DataProviderSynchronizer> _logger;
        private readonly PlayerUpdateRetryPolicy _retryPolicy;

        // Serializes queue drains so at most one runs at a time. The pub/sub background worker re-arms its wait
        // independently of callback completion, so a second wake arriving mid-drain can dispatch a second
        // ProcessQueue on another thread-pool thread; both then pop from the same queue via atomic LPOP and can
        // apply two order-sensitive same-player events (equip→unequip, the delete-then-rebuild loadout handler)
        // out of order, persisting stale state until the cache self-heals on the next save (#578). WaitAsync(0)
        // never materializes a wait handle, so the semaphore is intentionally left undisposed (this is a
        // process-lifetime hosted service anyway) — the same rationale as the per-socket command/send locks.
        private readonly SemaphoreSlim _drainGate = new(1, 1);

        // 0/1 wake flag, accessed only through Interlocked/Volatile. A caller sets it before trying to claim the
        // gate, and the active drainer re-checks it after releasing, so a wake that races the drainer's exit is
        // never stranded — it is coalesced into one follow-up pass rather than dropped.
        private int _drainRequested;

        // Last dead-letter depth surfaced to the log. Nothing drains the dead-letter queue automatically, so its
        // depth effectively only grows; logging only when it grows past this value keeps a standing poison count
        // from re-spamming the log on every drain. Touched only under the serialized drain, so it needs no lock.
        private long _lastReportedDeadLetterDepth;

        public DataProviderSynchronizer(IServiceProvider services, IPubSubService pubsub, ILogger<DataProviderSynchronizer> logger, PlayerUpdateRetryPolicy retryPolicy)
        {
            _services = services;
            _pubsub = pubsub;
            _logger = logger;
            _retryPolicy = retryPolicy;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await InitSubscriber();

            var queue = _pubsub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE);

            // Recover any items a previous run reserved but never acknowledged — it crashed (deploy, scale-down,
            // kill) mid-apply — before draining, so an event in flight when that run died is re-applied rather
            // than lost (#769). The reclaim is safe to run while other instances drain because the handlers are
            // idempotent, so at worst a still-live item is applied twice; cross-instance same-player ordering is
            // already only best-effort, so the reclaim introduces no new reordering hazard.
            var reclaimed = await queue.ReclaimProcessingAsync();
            if (reclaimed > 0)
            {
                _logger.LogInformation("Reclaimed {Count} in-flight player update(s) orphaned by a previous run on queue '{Queue}'.", reclaimed, Constants.PUBSUB_PLAYER_QUEUE);
            }

            // Drain whatever is already queued once on startup. Redis pub/sub wakes are at-most-once and
            // fire-and-forget (#552), so an item enqueued while no subscriber was connected — across an
            // instance restart, or a wake dropped during a brief subscriber outage — would otherwise wait
            // for the next publish to trigger a drain, stranding it at the tail when no further save follows.
            // Subscribing first ensures any item enqueued during the drain still gets a wake, and the drain
            // gate + coalescing flag in ProcessQueue serialize this with the subscription's own first wake
            // (the reserve/acknowledge read is idempotent, so a concurrent first wake can never double-apply) (#560).
            await ProcessQueue(queue);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task InitSubscriber()
        {
            await _pubsub.Subscribe(
                Constants.PUBSUB_PLAYER_CHANNEL,
                Constants.PUBSUB_PLAYER_QUEUE,
                async args => await ProcessQueue(args.queue));
        }

        internal async Task ProcessQueue(IPubSubQueue queue)
        {
            // Record the wake before attempting to claim the gate. If another drain is already in progress it
            // will observe this flag when it re-checks after releasing (below) and drain on our behalf, so the
            // events we were woken for are never missed even though we return without draining ourselves.
            Interlocked.Exchange(ref _drainRequested, 1);

            // Re-check after each release: a wake that set the flag while we held the gate failed to claim it
            // (WaitAsync(0) returned false for that caller), so we loop back to honor it rather than strand it.
            while (Volatile.Read(ref _drainRequested) == 1 && await _drainGate.WaitAsync(0))
            {
                try
                {
                    // Claim the pending wake(s) one pass at a time. Exchanging the flag to 0 before draining means
                    // a wake arriving during the drain re-sets it to 1, so the loop runs another pass and picks up
                    // the freshly enqueued events — the whole queue is drained without ever running two passes
                    // concurrently.
                    while (Interlocked.Exchange(ref _drainRequested, 0) == 1)
                    {
                        await DrainQueueAsync(queue);
                    }
                }
                finally
                {
                    _drainGate.Release();
                }
            }
        }

        private async Task DrainQueueAsync(IPubSubQueue queue)
        {
            var deadLetterQueue = _pubsub.GetQueue(Constants.PUBSUB_PLAYER_DEAD_LETTER_QUEUE);

            // Reserve each item (move it to the processing list) instead of destructively popping it, and only
            // acknowledge (remove it) once ProcessMessage has durably applied or dead-lettered it. A crash
            // anywhere in between leaves the item on the processing list to be reclaimed on next startup rather
            // than lost (#769). At-least-once is safe because the handlers are idempotent.
            var next = await queue.ReserveNextAsync();
            while (next is not null)
            {
                await ProcessMessage(next, deadLetterQueue);
                await queue.AcknowledgeAsync(next);
                next = await queue.ReserveNextAsync();
            }

            await SurfaceDeadLetterDepth(deadLetterQueue);
        }

        /// <summary>
        /// Surfaces the dead-letter queue's depth so accumulating poison messages don't pile up invisibly (#769).
        /// Nothing drains the dead-letter queue automatically, so a warning is logged only when its depth grows
        /// past the last reported value — keeping a standing count from spamming the log on every drain while
        /// still flagging fresh growth (and a standing backlog on the first drain after startup).
        /// </summary>
        private async Task SurfaceDeadLetterDepth(IPubSubQueue deadLetterQueue)
        {
            var depth = await deadLetterQueue.GetLengthAsync();
            if (depth > _lastReportedDeadLetterDepth)
            {
                _logger.LogWarning("Player update dead-letter queue '{Queue}' now holds {Count} message(s) awaiting inspection.", Constants.PUBSUB_PLAYER_DEAD_LETTER_QUEUE, depth);
            }

            _lastReportedDeadLetterDepth = depth;
        }

        /// <summary>
        /// Processes a single queued message. Malformed payloads (which can never succeed) are dead-lettered
        /// immediately, while a valid event that fails on an unexpected error (e.g. a transient database error)
        /// is retried with exponential backoff per <see cref="PlayerUpdateRetryPolicy"/> and dead-lettered only
        /// once the retries are exhausted, so the change is never silently dropped.
        /// </summary>
        private async Task ProcessMessage(string message, IPubSubQueue deadLetterQueue)
        {
            DomainEventEnvelope? envelope;
            try
            {
                envelope = message.Deserialize<DomainEventEnvelope>();
            }
            catch (JsonException ex)
            {
                // A malformed payload can never be parsed successfully, so it is dead-lettered for inspection rather than retried.
                _logger.LogWarning(ex, "Dead-lettering malformed player data event from queue '{Queue}'. Raw message: {Message}", Constants.PUBSUB_PLAYER_QUEUE, message);
                await deadLetterQueue.AddToQueueAsync(message);
                return;
            }

            if (envelope is null)
            {
                // A null payload deserialized cleanly but carries no event to apply; dead-letter it rather than silently dropping it.
                _logger.LogWarning("Dead-lettering empty player data event from queue '{Queue}'. Raw message: {Message}", Constants.PUBSUB_PLAYER_QUEUE, message);
                await deadLetterQueue.AddToQueueAsync(message);
                return;
            }

            for (var attempt = 1; attempt <= _retryPolicy.MaxAttempts; attempt++)
            {
                try
                {
                    await HandleEvent(envelope);
                    return;
                }
                catch (JsonException ex)
                {
                    // A malformed inner payload is a poison message that no retry can fix, so it is dead-lettered immediately.
                    _logger.LogWarning(ex, "Dead-lettering player data event '{EventType}' with a malformed payload from queue '{Queue}'. Raw message: {Message}", envelope.Type, Constants.PUBSUB_PLAYER_QUEUE, message);
                    await deadLetterQueue.AddToQueueAsync(message);
                    return;
                }
                catch (UnknownEventTypeException ex)
                {
                    // An unrecognized event type is a poison message — no retry can fix it — so it is dead-lettered immediately.
                    _logger.LogWarning(ex, "Dead-lettering player data event with unrecognized type '{EventType}' from queue '{Queue}'. Raw message: {Message}", envelope.Type, Constants.PUBSUB_PLAYER_QUEUE, message);
                    await deadLetterQueue.AddToQueueAsync(message);
                    return;
                }
                catch (Exception ex) when (attempt < _retryPolicy.MaxAttempts)
                {
                    // An unexpected failure (e.g. a transient database error) may succeed on a retry.
                    _logger.LogWarning(ex, "Failed to process player data event '{EventType}' from queue '{Queue}' on attempt {Attempt} of {MaxAttempts}; retrying.", envelope.Type, Constants.PUBSUB_PLAYER_QUEUE, attempt, _retryPolicy.MaxAttempts);
                    await Task.Delay(_retryPolicy.DelayAfterAttempt(attempt));
                }
                catch (Exception ex)
                {
                    // Retries exhausted: the change could not be persisted, so the event is dead-lettered for later inspection/replay instead of being dropped.
                    _logger.LogError(ex, "Failed to process player data event '{EventType}' from queue '{Queue}' after {MaxAttempts} attempts; dead-lettering. Raw message: {Message}", envelope.Type, Constants.PUBSUB_PLAYER_QUEUE, _retryPolicy.MaxAttempts, message);
                    await deadLetterQueue.AddToQueueAsync(message);
                    return;
                }
            }
        }

        // Applies a single dequeued event to the database. A fresh scope per call (and per retry attempt)
        // gives each apply its own GameContext, and the dispatcher resolves the discrete
        // IPlayerUpdateHandler for the envelope's type — an unregistered type surfaces as an
        // UnknownEventTypeException (dead-lettered without retry by ProcessMessage above).
        private async Task HandleEvent(DomainEventEnvelope envelope)
        {
            using var scope = _services.CreateScope();
            var dispatcher = new PlayerUpdateEventDispatcher(scope.ServiceProvider);
            await dispatcher.DispatchAsync(envelope);
        }
    }
}
