using Game.Abstractions.Infrastructure;
using Game.Core;
using Game.DataAccess.PlayerUpdates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Game.DataAccess
{
    internal class DataProviderSynchronizer : IHostedService, IDisposable
    {
        /// <summary>
        /// How long <see cref="StopAsync"/> waits for an in-flight drain to reach a clean item boundary and
        /// release the gate before giving up. Sized well under the host's default 30s shutdown timeout so the
        /// give-up path runs (and is observable) rather than the host force-killing the process first; anything
        /// left unprocessed is reclaimed and re-applied on the next startup, so the bound loses nothing.
        /// </summary>
        private static readonly TimeSpan DefaultDrainTimeout = TimeSpan.FromSeconds(5);

        /// <summary>
        /// How long the processing list's head item must persist there unacknowledged (with nothing left to
        /// reserve from the main queue) before it is opportunistically reclaimed. Comfortably longer than a
        /// healthy apply — including its retry backoff (<see cref="PlayerUpdateRetryPolicy"/>) — so this
        /// doesn't routinely race a genuinely in-flight item on another instance.
        /// </summary>
        private static readonly TimeSpan DefaultReclaimGracePeriod = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Default upper bound on how many player-update events this instance applies concurrently within one
        /// drain pass. Same-player ordering is preserved regardless (see <see cref="ProcessReservedItemAsync"/>),
        /// so this only relaxes the cross-player serialization that otherwise ceilings single-instance
        /// convergence throughput at one DB round-trip per event (#1701). Conservative default; tune via the
        /// constructor for a fleet under heavier convergence load.
        /// </summary>
        private const int DefaultMaxConcurrentDrainItems = 4;

        /// <summary>
        /// Lane key for a reserved item whose player id couldn't be determined (a malformed envelope/payload).
        /// Such items are rare and are routed onto one shared serial lane rather than their own concurrent
        /// slot — always order-safe since a genuine player id never collides with it.
        /// </summary>
        private const int UnknownPlayerLane = int.MinValue;

        private readonly IServiceProvider _services;
        private readonly IPubSubService _pubsub;
        private readonly ILogger<DataProviderSynchronizer> _logger;
        private readonly PlayerUpdateRetryPolicy _retryPolicy;
        private readonly TimeSpan _drainTimeout;
        private readonly TimeSpan _reclaimGracePeriod;
        private readonly TimeProvider _timeProvider;
        private readonly int _maxConcurrentDrainItems;

        // When a drain pass finds the main queue empty but the processing list non-empty, this is the identity
        // and first-observed instant of whichever item currently sits at the processing list's head (its
        // oldest, least-recently-reserved entry). Reset to null whenever the processing list is seen empty
        // (including right after an opportunistic reclaim), or when a different item now occupies the head
        // (the tracked one was acknowledged/reclaimed and something else took its place). Tracking the
        // specific item — rather than mere list occupancy — means a busy multi-instance deployment, where the
        // shared list rarely reads empty, only reclaims an item that has itself dwelled at the head past the
        // grace period, instead of treating steady per-instance churn as staleness. Touched only under the
        // serialized drain (the gate below), so it needs no lock — the same rationale as
        // _lastReportedDeadLetterDepth.
        private (string Value, DateTimeOffset ObservedAt)? _strandedProcessingHead;

        // Cancelled on shutdown to signal any in-flight drain (the startup drain or a pub/sub wake) to stop
        // reserving new items and unwind at a clean item boundary, and to make a late wake a no-op.
        private readonly CancellationTokenSource _stopping = new();
        private bool _disposed;

        // Identifies this instance's worker subscription so StopAsync can tear it down (disposing the worker's OS
        // wait handle) rather than leaving it to leak — the worker-backed Subscribe overloads now require an id
        // for exactly this reason (#954), mirroring ReferenceCacheSynchronizer's id-scoped subscription.
        private string InstanceId { get; } = Guid.NewGuid().ToString();

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

        public DataProviderSynchronizer(
            IServiceProvider services,
            IPubSubService pubsub,
            ILogger<DataProviderSynchronizer> logger,
            PlayerUpdateRetryPolicy retryPolicy,
            TimeSpan? drainTimeout = null,
            TimeSpan? reclaimGracePeriod = null,
            TimeProvider? timeProvider = null,
            int? maxConcurrentDrainItems = null)
        {
            _services = services;
            _pubsub = pubsub;
            _logger = logger;
            _retryPolicy = retryPolicy;
            _drainTimeout = drainTimeout ?? DefaultDrainTimeout;
            _reclaimGracePeriod = reclaimGracePeriod ?? DefaultReclaimGracePeriod;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _maxConcurrentDrainItems = maxConcurrentDrainItems ?? DefaultMaxConcurrentDrainItems;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await InitSubscriber();

            var queue = _pubsub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE);

            // A stop requested mid-boot (host shutdown during a large reclaim) must unwind the startup work
            // too, so honor both the host's startup token and our own stopping signal. It is threaded into the
            // reclaim (which honors it cooperatively) so a wedged Redis round-trip unwinds promptly.
            using var startupCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _stopping.Token);
            var startupToken = startupCts.Token;

            try
            {
                if (startupToken.IsCancellationRequested)
                {
                    return;
                }

                // Recover any items a previous run reserved but never acknowledged — it crashed (deploy, scale-down,
                // kill) mid-apply — before draining, so an event in flight when that run died is re-applied rather
                // than lost (#769). The reclaim is safe to run while other instances drain because the handlers are
                // idempotent, so at worst a still-live item is applied twice; cross-instance same-player ordering is
                // already only best-effort, so the reclaim introduces no new reordering hazard.
                var reclaimed = await queue.ReclaimProcessingAsync(startupToken);
                if (reclaimed > 0)
                {
                    _logger.LogInformation("Reclaimed {Count} in-flight player update(s) orphaned by a previous run on queue '{Queue}'.", reclaimed, Constants.PUBSUB_PLAYER_QUEUE);
                }
            }
            catch (OperationCanceledException) when (startupToken.IsCancellationRequested)
            {
                // A stop requested mid-boot unwinds a wedged reclaim promptly via the now-cancelable queue op;
                // a clean unwind, not a startup failure. Anything left is reclaimed on the next startup, so
                // nothing is lost.
                return;
            }

            if (_stopping.IsCancellationRequested)
            {
                return;
            }

            // Drain whatever is already queued, dispatched to the background rather than awaited here. ASP.NET
            // Core holds Kestrel closed until every hosted service's StartAsync completes, and this queue is
            // fleet-shared — awaiting the whole backlog inline would block this instance's readiness on work a
            // rolling deploy's already-healthy instances may already be draining (#2096). The correctness goal
            // (an item enqueued while no subscriber was connected — Redis pub/sub wakes are at-most-once and
            // fire-and-forget, #552 — is still applied promptly rather than stranded until the next publish)
            // only needs subscribe + reclaim to precede the drain, not the drain itself to finish before
            // startup completes. Subscribing first (above) still ensures any item enqueued during this pass
            // gets its own wake, and the drain gate + coalescing flag in ProcessQueue still serialize this pass
            // with the subscription's own first wake (the reserve/acknowledge read is idempotent, so a
            // concurrent first wake can never double-apply) (#560).
            _ = RunStartupDrain(queue);
        }

        // Runs the startup drain off the host-readiness path (see StartAsync). Nothing awaits this task, so a
        // stop is honored via the instance's own stopping token — the host's startup token is no longer
        // meaningful once StartAsync has returned — and any exception that escapes ProcessQueue is logged here
        // rather than being lost as an unobserved task fault. StopAsync still waits on the drain gate that
        // ProcessQueue acquires, so shutdown continues to wait for this pass (within its bounded timeout)
        // exactly as it already does for a pub/sub-triggered drain.
        private async Task RunStartupDrain(IPubSubQueue queue)
        {
            try
            {
                await ProcessQueue(queue, _stopping.Token);
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                // A stop requested while the background drain was running unwinds cleanly; anything left
                // queued (or reserved but not yet acknowledged) is reclaimed and re-applied on the next startup.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception draining the player update queue on startup.");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // Remove this instance's worker subscription first so no further wakes arrive mid-drain, disposing the
            // worker's OS wait handle (id-scoped, so it tears down only this handler) — the teardown the leaked
            // id-less subscription could never reach (#954).
            await _pubsub.UnSubscribe(InstanceId);

            // Signal the in-flight drain (startup or a pub/sub wake) to stop reserving new items and unwind at
            // a clean item boundary, then wait for it to release the gate so an in-progress apply finishes
            // cleanly rather than being cut off. The wait is bounded so a stuck drain can't hold up host
            // shutdown — anything left queued (or an item reserved but not yet acknowledged) is reclaimed and
            // re-applied on the next boot, so the bound loses nothing.
            _stopping.Cancel();

            if (await _drainGate.WaitAsync(_drainTimeout))
            {
                _drainGate.Release();
            }
            else
            {
                _logger.LogWarning("Player update drain did not complete within {Timeout} on shutdown; remaining items will be reclaimed on the next startup.", _drainTimeout);
            }
        }

        private async Task InitSubscriber()
        {
            await _pubsub.Subscribe(
                Constants.PUBSUB_PLAYER_CHANNEL,
                Constants.PUBSUB_PLAYER_QUEUE,
                async args => await ProcessQueue(args.queue, _stopping.Token),
                InstanceId);
        }

        internal async Task ProcessQueue(IPubSubQueue queue, CancellationToken cancellationToken = default)
        {
            // Record the wake before attempting to claim the gate. If another drain is already in progress it
            // will observe this flag when it re-checks after releasing (below) and drain on our behalf, so the
            // events we were woken for are never missed even though we return without draining ourselves.
            Interlocked.Exchange(ref _drainRequested, 1);

            // Re-check after each release: a wake that set the flag while we held the gate failed to claim it
            // (WaitAsync(0) returned false for that caller), so we loop back to honor it rather than strand it.
            // A requested stop short-circuits the whole thing so a late wake during shutdown is a no-op.
            while (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _drainRequested) == 1 && await _drainGate.WaitAsync(0))
            {
                try
                {
                    // Claim the pending wake(s) one pass at a time. Exchanging the flag to 0 before draining means
                    // a wake arriving during the drain re-sets it to 1, so the loop runs another pass and picks up
                    // the freshly enqueued events — the whole queue is drained without ever running two passes
                    // concurrently.
                    while (!cancellationToken.IsCancellationRequested && Interlocked.Exchange(ref _drainRequested, 0) == 1)
                    {
                        await DrainQueueAsync(queue, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // A stop requested mid-drain unwinds a wedged reserve promptly via the now-cancelable read;
                    // this is the same clean stop as the boundary check (anything left is reclaimed and re-applied
                    // on the next startup), so it is swallowed rather than surfaced as an error by the pub/sub
                    // worker loop, which would otherwise log every escaping exception.
                }
                finally
                {
                    _drainGate.Release();
                }
            }
        }

        private async Task DrainQueueAsync(IPubSubQueue queue, CancellationToken cancellationToken)
        {
            var deadLetterQueue = _pubsub.GetQueue(Constants.PUBSUB_PLAYER_DEAD_LETTER_QUEUE);

            // Reserve each item (move it to the processing list) instead of destructively popping it, and only
            // acknowledge (remove it) once ProcessMessage has durably applied or dead-lettered it. A crash
            // anywhere in between leaves the item on the processing list to be reclaimed on next startup rather
            // than lost (#769). At-least-once is safe because the handlers are idempotent.
            //
            // Reservation itself stays strictly sequential (one ReserveNextAsync at a time, in order), but the
            // apply+acknowledge of already-reserved items runs with bounded concurrency across DISTINCT players
            // (#1701) — cross-instance ordering was already only best-effort, so this introduces no new hazard
            // there. Same-player items still apply strictly in order: each is chained onto a per-player "lane"
            // (playerLanes) that only starts an item once the previous same-player item has fully applied and
            // acknowledged. The concurrency gate is acquired before reserving, not just before processing, so
            // reservation itself is bounded by the same budget — a stop still ends at a bounded (not unbounded)
            // number of in-flight items, reclaimed on the next startup if the drain timeout is exceeded. Once an
            // item is reserved its apply and acknowledge run without the token so the in-flight write finishes
            // cleanly — only the dead-time retry backoff between failed attempts honors the token (a stop during
            // it abandons the retry, and the reserved item is reclaimed and re-applied on the next startup).
            var playerLanes = new Dictionary<int, Task>();
            var inFlight = new List<Task>();
            using var concurrencyGate = new SemaphoreSlim(_maxConcurrentDrainItems);

            // Sweep completed entries out of both collections once they grow past this many, so a large-backlog
            // pass — the exact case #1701 targets — retains roughly the concurrency budget rather than the
            // whole pass's item count. Checked periodically rather than every item so the amortized sweep cost
            // stays O(pass size) overall instead of O(pass size squared).
            var compactionThreshold = Math.Max(_maxConcurrentDrainItems * 8, 32);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await concurrencyGate.WaitAsync(cancellationToken);

                    // WaitAsync(CancellationToken) can win a race against its own token: if the slot is
                    // released at roughly the same moment the token is cancelled, the wait can complete by
                    // acquiring the slot instead of throwing OperationCanceledException. Re-checking here
                    // closes that race deterministically — a stop never reserves one item past the boundary
                    // depending on how that race happened to resolve.
                    if (cancellationToken.IsCancellationRequested)
                    {
                        concurrencyGate.Release();
                        break;
                    }

                    string? next;
                    try
                    {
                        next = await queue.ReserveNextAsync(cancellationToken);
                    }
                    catch
                    {
                        concurrencyGate.Release();
                        throw;
                    }

                    if (next is null)
                    {
                        concurrencyGate.Release();

                        // Nothing left to reserve. Before consulting the stranded-processing reclaim (#1702),
                        // settle everything this pass itself still has in flight — an item legitimately sits on
                        // the processing list while it applies, so counting our own in-flight work would start
                        // (or trip) the stranded clock for items that aren't stranded at all. The barrier costs
                        // nothing throughput-wise: the queue is empty, so there is no other work this loop
                        // could be overlapping with.
                        await SettleInFlightItemsAsync(playerLanes, inFlight);

                        // Opportunistically reclaim anything genuinely stranded on the processing list (past
                        // the grace period) instead of waiting for the next process restart to recover it
                        // (#1702), and keep draining if that surfaced fresh work. The settle above reset the
                        // lanes, so a reclaimed item — including one whose own apply faulted moments ago —
                        // re-runs on a fresh lane rather than chaining onto its predecessor's dead lane and
                        // re-faulting without ever reaching ProcessMessage.
                        if (await TryReclaimStrandedProcessingAsync(queue, cancellationToken))
                        {
                            continue;
                        }

                        break;
                    }

                    var playerId = PlayerUpdateEnvelopeReader.TryReadPlayerId(next) ?? UnknownPlayerLane;
                    var previous = playerLanes.TryGetValue(playerId, out var existingLane) ? existingLane : Task.CompletedTask;
                    var itemTask = ProcessReservedItemAsync(previous, next, queue, deadLetterQueue, concurrencyGate, cancellationToken);
                    playerLanes[playerId] = itemTask;
                    inFlight.Add(itemTask);

                    if (inFlight.Count >= compactionThreshold)
                    {
                        CompactCompletedItems(playerLanes, inFlight);
                    }
                }
            }
            finally
            {
                // Always await every item this pass started — including one still mid-flight when cancellation
                // unwinds the reserve loop above (e.g. a stop mid-wait-for-a-slot) — so a stop genuinely waits
                // for an in-progress apply to finish. Without this, ProcessQueue's caller could release the
                // drain gate (and StopAsync could return) while an item was still applying, which is exactly
                // the "cut off mid-SaveChanges" outcome the gate exists to prevent (docs/backend-persistence.md).
                // The settle never throws, so an exception already unwinding this frame is never masked.
                await SettleInFlightItemsAsync(playerLanes, inFlight);
            }

            // Skip the extra Redis round-trip if we stopped early; the depth is surfaced again on the next drain.
            if (!cancellationToken.IsCancellationRequested)
            {
                await SurfaceDeadLetterDepth(deadLetterQueue);
            }
        }

        /// <summary>
        /// Drops finished tasks from <paramref name="inFlight"/> and <paramref name="playerLanes"/> so a
        /// large-backlog pass retains roughly the concurrency budget rather than growing to the whole pass's
        /// item count. Only <em>successfully</em> completed tasks are evicted: a faulted (or canceled) lane
        /// head must stay in the map so a later same-player item chains onto it and faults in turn — staying
        /// reserved for the reclaim — rather than starting a fresh lane and applying <em>ahead</em> of the
        /// failed item's eventual reclaim/re-apply, which would break same-player ordering exactly on the
        /// fault path. Keeping them in <paramref name="inFlight"/> likewise keeps the drain-exit settle aware
        /// of them. A lane is only evicted when its <em>current</em> (most recently chained) task has
        /// completed — a still-running or not-yet-started successor for that player is left untouched, so a
        /// later item for the same player still correctly chains onto it rather than a stale completed entry.
        /// </summary>
        private static void CompactCompletedItems(Dictionary<int, Task> playerLanes, List<Task> inFlight)
        {
            inFlight.RemoveAll(task => task.IsCompletedSuccessfully);

            List<int>? completedLanes = null;
            foreach (var (playerId, task) in playerLanes)
            {
                if (task.IsCompletedSuccessfully)
                {
                    (completedLanes ??= []).Add(playerId);
                }
            }

            if (completedLanes is null)
            {
                return;
            }

            foreach (var playerId in completedLanes)
            {
                playerLanes.Remove(playerId);
            }
        }

        /// <summary>
        /// Awaits every item task this pass has started, then resets both tracking collections so subsequent
        /// work starts on fresh lanes. Never throws: an item task only faults when a Redis queue op (the
        /// acknowledge or a dead-letter enqueue) escapes <see cref="ProcessMessage"/>, or when its predecessor
        /// on the same lane did — either way the item is still reserved on the processing list, so the
        /// stranded-processing reclaim (or the next startup) re-applies it rather than losing it. Faults are
        /// logged here once; cancellations (a stop mid-retry-backoff) are the normal shutdown contract and
        /// need no logging.
        /// </summary>
        private async Task SettleInFlightItemsAsync(Dictionary<int, Task> playerLanes, List<Task> inFlight)
        {
            if (inFlight.Count > 0)
            {
                try
                {
                    await Task.WhenAll(inFlight);
                }
                catch (Exception ex) when (inFlight.Any(task => task.IsFaulted))
                {
                    _logger.LogError(ex,
                        "{Count} player update(s) faulted while applying on queue '{Queue}'; they remain reserved and will be reclaimed and re-applied.",
                        inFlight.Count(task => task.IsFaulted), Constants.PUBSUB_PLAYER_QUEUE);
                }
                catch (OperationCanceledException)
                {
                    // Only canceled items (a stop abandoning a retry backoff); they stay reserved and are
                    // reclaimed on the next startup — the standard stop contract.
                }
            }

            playerLanes.Clear();
            inFlight.Clear();
        }

        /// <summary>
        /// Applies and acknowledges one reserved item once <paramref name="previous"/> (the same player's prior
        /// item, or an already-completed task when this is the first/only item for its player this pass)
        /// finishes, then releases the concurrency slot <see cref="DrainQueueAsync"/> acquired for it before
        /// reserving. The slot is held for this item's whole lifetime, including any wait on
        /// <paramref name="previous"/> — a deliberate cost: it is what keeps a backlog dominated by one hot
        /// player from letting <see cref="DrainQueueAsync"/> reserve unboundedly far ahead of what is actually
        /// converging, so a stop mid-drain still ends at a bounded number of in-flight items rather than an
        /// unbounded one.
        /// </summary>
        private async Task ProcessReservedItemAsync(Task previous, string message, IPubSubQueue queue, IPubSubQueue deadLetterQueue, SemaphoreSlim concurrencyGate, CancellationToken cancellationToken)
        {
            try
            {
                await previous;
                await ProcessMessage(message, deadLetterQueue, cancellationToken);
                await queue.AcknowledgeAsync(message);
            }
            finally
            {
                concurrencyGate.Release();
            }
        }

        /// <summary>
        /// Reclaims items stranded on the processing list — orphaned not by a crash (the startup reclaim's
        /// job) but by <c>ProcessMessage</c>'s own escape paths faulting after a durable apply (the dead-letter
        /// write or the acknowledge itself hitting a Redis blip) — once the item actually sitting at the
        /// processing list's head has persisted there, with nothing left on the main queue, for at least
        /// <see cref="_reclaimGracePeriod"/>. Tracking is keyed to that specific head item rather than mere
        /// list occupancy: a busy multi-instance deployment can keep the shared list continuously non-empty
        /// with items other instances are genuinely working through, and a clock keyed only on "is the list
        /// non-empty" would never reset there, eventually reclaiming healthy in-flight work. Comparing the head
        /// item across polls restarts the clock whenever a different item takes its place, so a reclaim only
        /// fires once one item has genuinely dwelled past the grace period — which exists only to avoid
        /// needlessly racing an item another live instance is still applying; reclaiming it regardless would
        /// still be safe under the queue's idempotent at-least-once contract, so reclaiming the whole list (not
        /// just the stale head item) once that threshold is crossed is safe too, and cheaper than reclaiming
        /// one item at a time. Returns whether anything was reclaimed, so the caller knows to keep draining.
        /// Callers must settle their own in-flight items first — this reads the shared processing list, which
        /// cannot distinguish a stranded item from one this pass is still applying.
        /// </summary>
        private async Task<bool> TryReclaimStrandedProcessingAsync(IPubSubQueue queue, CancellationToken cancellationToken)
        {
            var head = await queue.PeekProcessingAsync(1, cancellationToken);
            if (head.Count == 0)
            {
                _strandedProcessingHead = null;
                return false;
            }

            var now = _timeProvider.GetUtcNow();
            if (_strandedProcessingHead is not { } tracked || tracked.Value != head[0])
            {
                // Either the first sighting of a non-empty processing list, or a different item now occupies
                // the head (the previously-tracked one was acknowledged/reclaimed) — start its clock rather
                // than reclaiming immediately.
                _strandedProcessingHead = (head[0], now);
                return false;
            }

            if (now - tracked.ObservedAt < _reclaimGracePeriod)
            {
                return false;
            }

            var reclaimed = await queue.ReclaimProcessingAsync(cancellationToken);
            _strandedProcessingHead = null;
            if (reclaimed > 0)
            {
                _logger.LogInformation(
                    "Opportunistically reclaimed {Count} player update(s) stranded on the processing list for over {GracePeriod} on queue '{Queue}'.",
                    reclaimed, _reclaimGracePeriod, Constants.PUBSUB_PLAYER_QUEUE);
            }

            return reclaimed > 0;
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
        /// once the retries are exhausted, so the change is never silently dropped. The apply itself runs
        /// uncancelled (so a reserved item finishes cleanly); only the dead-time backoff between failed
        /// attempts honors <paramref name="cancellationToken"/>, so a shutdown isn't stalled waiting one out.
        /// </summary>
        private async Task ProcessMessage(string message, IPubSubQueue deadLetterQueue, CancellationToken cancellationToken)
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
                    // An unexpected failure (e.g. a transient database error) may succeed on a retry. The
                    // backoff is dead time, not an in-flight write, so a shutdown cancels it rather than
                    // waiting it out against the bounded drain budget — the reserved item is reclaimed next startup.
                    _logger.LogWarning(ex, "Failed to process player data event '{EventType}' from queue '{Queue}' on attempt {Attempt} of {MaxAttempts}; retrying.", envelope.Type, Constants.PUBSUB_PLAYER_QUEUE, attempt, _retryPolicy.MaxAttempts);
                    await Task.Delay(_retryPolicy.DelayAfterAttempt(attempt), cancellationToken);
                }
                catch (Exception ex)
                {
                    // Retries exhausted: the change could not be persisted, so the event is dead-lettered for later inspection/replay instead of being dropped.
                    _logger.LogError(ex, "Failed to process player data event '{EventType}' from queue '{Queue}' after {MaxAttempts} attempts; dead-lettering. Raw message: {Message}", envelope.Type, Constants.PUBSUB_PLAYER_QUEUE, _retryPolicy.MaxAttempts, message);
                    await deadLetterQueue.AddToQueueAsync(message);
                    return;
                }
            }

            // Defensive terminal guard: every attempt above either returns (success or dead-letter) or, on the
            // final attempt, falls into the retries-exhausted catch which dead-letters and returns — so with at
            // least one attempt (RetryPolicy enforces MaxAttempts >= 1) control never reaches here. Dead-letter
            // rather than fall through anyway, so a future change to the attempt bounds can never silently turn
            // this terminal path into a dropped player write (#937).
            _logger.LogError("Player data event '{EventType}' from queue '{Queue}' reached the end of the retry loop without a terminal action; dead-lettering defensively. Raw message: {Message}", envelope.Type, Constants.PUBSUB_PLAYER_QUEUE, message);
            await deadLetterQueue.AddToQueueAsync(message);
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

        public void Dispose()
        {
            // The container disposes this singleton once on shutdown (after StopAsync), so a guard is belt-and-
            // suspenders. The drain gate is intentionally left undisposed (see its field comment).
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stopping.Dispose();
        }
    }
}
