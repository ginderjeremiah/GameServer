using Game.Abstractions.Infrastructure;
using Game.Api.Sockets;
using Game.Api.Sockets.Commands;
using Game.Core;
using System.Net.WebSockets;

namespace Game.Api.Services
{
    public class SocketManagerService
    {
        private readonly IPubSubService _pubSub;
        private readonly ICacheService _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<SocketManagerService> _logger;
        private readonly SocketCommandFactory _commandFactory;
        private readonly SocketConnectionRegistry _socketRegistry;

        /// <summary>
        /// Time-to-live on the per-player socket-presence key. The client heartbeats every 10s
        /// (<c>api-socket.ts</c>), and every inbound message refreshes this TTL, so a live connection
        /// keeps its key fresh while a non-gracefully-closed one (a tab/device that vanished without a
        /// close frame) lets the key expire within this window instead of lingering until the 60s
        /// inactivity check. That keeps the <see cref="HasActiveSocket"/> presence signal honest — the
        /// 30s budget tolerates two missed heartbeats before a live session is mistaken for gone.
        /// </summary>
        private static readonly TimeSpan SocketPresenceTtl = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Backstop TTL applied to a per-socket command queue (<see cref="SocketQueueName"/>) on every push.
        /// The queue is a plain Redis list with no expiry of its own, so a push that races a disconnect (the
        /// presence key not yet released), lands after <see cref="UnRegisterSocketCommandListener"/>, or
        /// arrives after the processor abandoned its drain loop (<see cref="MaxConsecutiveDequeueFailures"/>)
        /// would otherwise strand a permanent, TTL-less key. <see cref="TeardownSocketRegistration"/> deletes
        /// the key outright on the common graceful-disconnect path; this TTL is the fallback for every path
        /// that skips teardown (a crash, a hard disconnect) — generous enough that it never bites a queue a
        /// live socket is still draining (drain latency is milliseconds), since it only needs to bound the
        /// worst case rather than track a live connection's lifetime.
        /// </summary>
        private static readonly TimeSpan SocketQueueTtl = TimeSpan.FromHours(1);

        /// <summary>
        /// Sentinel value <see cref="TryClaimForSwitchCredit"/> writes into a presence key: not a real socket
        /// id, so <see cref="RegisterSocket"/> must recognize it (rather than notifying it as a replaced
        /// connection) and <see cref="CurrentSocketId"/>'s callers must never treat it as a live socket.
        /// </summary>
        private const string SwitchCreditClaimValue = "switch-credit";

        /// <summary>
        /// TTL on a switch-away credit's claim (see <see cref="TryClaimForSwitchCredit"/>). Generous relative to
        /// the credit's actual work (a player load plus an offline-progress simulation bounded well under the
        /// socket command timeout) so it only bites a crashed credit that never released its claim, bounding how
        /// long <see cref="RegisterSocket"/> will ever defer behind one.
        /// </summary>
        private static readonly TimeSpan SwitchCreditClaimTtl = TimeSpan.FromSeconds(20);

        /// <summary>Poll interval <see cref="RegisterSocket"/> uses while waiting out a switch-credit claim.</summary>
        private static readonly TimeSpan SwitchCreditWaitPollInterval = TimeSpan.FromMilliseconds(50);

        public SocketManagerService(IPubSubService pubSub, ICacheService cache, SocketCommandFactory commandFactory, IServiceScopeFactory scopeFactory, ILoggerFactory loggerFactory, SocketConnectionRegistry socketRegistry)
        {
            _pubSub = pubSub;
            _cache = cache;
            _scopeFactory = scopeFactory;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<SocketManagerService>();
            _commandFactory = commandFactory;
            _socketRegistry = socketRegistry;
        }

        public async Task<SocketContext> RegisterSocket(WebSocket socket, SessionService sessionService, bool isAdmin)
        {
            var playerId = sessionService.SelectedPlayerId;
            var socketContext = new SocketContext(socket, playerId, sessionService, isAdmin, _loggerFactory.CreateLogger<SocketContext>());
            var socketHandler = new SocketHandler(socketContext, _commandFactory, _scopeFactory, _loggerFactory.CreateLogger<SocketHandler>(), () => RefreshSocketPresence(playerId, socketContext.SocketId));
            var presenceKey = CurrentSocketKey(playerId);
            // Defer behind an in-flight switch-away credit's claim (#2041) rather than overwriting it: that
            // credit is a read-modify-write against this same player's aggregate run off this player's battle
            // loop, so registering (and starting the loop) while it's still mid-flight would reintroduce the
            // lost-update race the presence claim exists to prevent. ClaimPresenceKey's CompareAndSet loop
            // closes the gap a separate wait-then-unconditional-write would leave between "the claim looked
            // clear" and "the key was written" — see its doc comment.
            var oldSocketId = await ClaimPresenceKey(presenceKey, socketContext.SocketId);

            try
            {
                await RegisterSocketCommandListener(socketHandler);
                // Register before starting the loops so the registry tracks the socket — and threads its
                // shutdown tokens into Listen — for a graceful drain on host shutdown (#526). Awaited because
                // a socket arriving mid-drain is closed cleanly here rather than tracked-but-undrained (#904).
                await _socketRegistry.Register(socketHandler);
            }
            catch
            {
                // A step after the presence-key write failed, so the key now points at a socket whose drain
                // loops never started — a "registered but dead" presence that would block the player and never
                // drain. Undo the partial registration before propagating.
                await TeardownSocketRegistration(socketContext, "rolling back a failed registration");
                throw;
            }

            // Signal the replaced connection to close only now that the new socket is fully established.
            // Emitting it before the registration above would, on a transient fault there (which rolls the new
            // registration back), leave the player with a closing old connection and no working new one. This
            // is best-effort: a failure here just leaves the old socket to its own inactivity teardown rather
            // than tearing down the live new connection over a notification the old socket no longer needs.
            if (oldSocketId is not null)
            {
                try
                {
                    await EmitSocketCommand(new SocketReplacedInfo(), oldSocketId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to notify replaced socket {OldSocketId} for player {PlayerId}; it will be cleaned up by its own inactivity teardown.", oldSocketId, playerId);
                }
            }

            _logger.LogDebug("Initiated socket for player: ({Id}), with Id: {SocketId}", playerId, socketContext.SocketId);
            return socketContext;
        }

        /// <summary>
        /// Best-effort teardown of a socket's registration — used both for a clean disconnect
        /// (<see cref="UnRegisterSocket"/>) and to roll back a partially-completed <see cref="RegisterSocket"/>.
        /// Drops registry tracking, unsubscribes the command listener, and releases the presence claim (only
        /// if it is still ours). Each awaited step is guarded so a fault in one can't skip the others — most
        /// importantly the presence-key release, which a ghost session would otherwise survive on until its
        /// TTL — and so a cleanup fault on the rollback path can't mask the registration exception about to
        /// propagate. <paramref name="reason"/> labels the teardown in the failure logs.
        /// </summary>
        private async Task TeardownSocketRegistration(SocketContext context, string reason)
        {
            _socketRegistry.Unregister(context.SocketId);
            try
            {
                await UnRegisterSocketCommandListener(context.SocketId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unsubscribe socket {SocketId} while {Reason}.", context.SocketId, reason);
            }

            try
            {
                // Prompt cleanup for the common graceful-disconnect path — the socket's own queue key, unlike
                // the shared presence key, is never contended by another socket, so a plain delete (rather than
                // a compare-and-delete) is safe. SocketQueueTtl is only the fallback for paths that skip teardown.
                await _cache.Delete(SocketQueueName(context.SocketId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete the command queue for socket {SocketId} while {Reason}.", context.SocketId, reason);
            }

            try
            {
                // Compare-and-delete so we only release the key while it is still ours — a newer connection may
                // have taken it over, and that key must be left intact.
                await _cache.CompareAndDelete(CurrentSocketKey(context.PlayerId), context.SocketId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release the presence key for socket {SocketId} while {Reason}.", context.SocketId, reason);
            }
        }

        /// <summary>
        /// Returns whether the player currently has a live socket registered, without opening or
        /// touching one. The login flow uses this to warn the user before a new connection takes over
        /// an existing session. Also reports <see langword="true"/> for the brief window a switch-away
        /// credit holds the key (see <see cref="TryClaimForSwitchCredit"/>) — a harmless false positive for
        /// this warning-only use, since that window is milliseconds and self-clears.
        /// </summary>
        public async Task<bool> HasActiveSocket(int playerId)
        {
            return await CurrentSocketId(playerId) is not null;
        }

        /// <summary>
        /// Atomically claims <paramref name="playerId"/>'s presence key for an in-flight switch-away credit
        /// (<c>LoginController.CreditDepartedCharacter</c>, #2041), replacing a plain presence read: the read
        /// and the claim happen in one Redis round trip, so there is no gap between "no socket is here" and
        /// "reserve this slot" for a concurrent <see cref="RegisterSocket"/> to land in. Returns
        /// <see langword="false"/> when the key is already held — a genuinely live socket (the credit must be
        /// skipped, its battle loop owns the saves) or another switch-credit already claiming it.
        /// </summary>
        public async Task<bool> TryClaimForSwitchCredit(int playerId)
        {
            return await _cache.CompareAndSet(CurrentSocketKey(playerId), null, SwitchCreditClaimValue, SwitchCreditClaimTtl);
        }

        /// <summary>
        /// Releases a switch-away credit's claim taken by <see cref="TryClaimForSwitchCredit"/>. Compare-and-
        /// delete so it only clears the key while the claim is still ours — a concurrent <see cref="RegisterSocket"/>
        /// may have already kicked it and claimed the key for a real socket, which must be left intact.
        /// </summary>
        public async Task ReleaseSwitchCreditClaim(int playerId)
        {
            await _cache.CompareAndDelete(CurrentSocketKey(playerId), SwitchCreditClaimValue);
        }

        /// <summary>
        /// Atomically claims <paramref name="presenceKey"/> as <paramref name="newSocketId"/> via a
        /// CompareAndSet loop, so a switch-away credit's claim (#2041) landing in the gap between reading the
        /// key and writing it can never be kicked: the write only lands if the key's value is still what was
        /// just read, so a claim (or another registration) that lands first makes the CompareAndSet fail, and
        /// the loop re-reads and retries against whatever is there now instead of blindly overwriting it — the
        /// gap a separate wait-then-unconditional-write would leave open. While the read value is the claim
        /// sentinel, the loop defers (polling at <see cref="SwitchCreditWaitPollInterval"/>) rather than racing
        /// it, capped at <see cref="SwitchCreditClaimTtl"/> — the claim's own TTL — so a credit that faults
        /// without releasing can never wedge a registration past that bound; only once that deadline passes
        /// does an attempt targeting the (by then stale) sentinel proceed. Returns the previous real socket id,
        /// or <see langword="null"/> if the key was unset or held only a switch-credit claim.
        /// </summary>
        private async Task<string?> ClaimPresenceKey(string presenceKey, string newSocketId)
        {
            var deadline = DateTime.UtcNow + SwitchCreditClaimTtl;
            var currentValue = await _cache.Get(presenceKey);
            while (true)
            {
                if (currentValue == SwitchCreditClaimValue && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(SwitchCreditWaitPollInterval);
                    currentValue = await _cache.Get(presenceKey);
                    continue;
                }

                if (await _cache.CompareAndSet(presenceKey, currentValue, newSocketId, SocketPresenceTtl))
                {
                    return currentValue == SwitchCreditClaimValue ? null : currentValue;
                }

                // Another writer (a fresh switch-credit claim, a competing registration) won the race between
                // our read and this write; re-read whatever landed and retry against that instead. Unbounded by
                // design but self-limiting in practice: each failure here means one more concurrent writer for
                // this single player's key, a set that's small and finite rather than something that can churn
                // forever.
                currentValue = await _cache.Get(presenceKey);
            }
        }

        /// <summary>
        /// Extends the player's socket-presence TTL on connection activity, so a live socket keeps its
        /// presence key from expiring (see <see cref="SocketPresenceTtl"/>). Reclaims the key as this
        /// socket's own id if it is currently unset, so a live socket can restore a claim that lapsed (a TTL
        /// lapse from an inbound stall) or was rolled back out from under it (a superseding registration
        /// that later failed) — a bare expire is a no-op on a missing key and can never resurrect it. If the
        /// key already holds a different (newer) socket's id, the refresh still just extends that key's TTL
        /// without touching its value, so it can never clobber a takeover. Fire-and-forget: presence refresh
        /// is best-effort (a missed refresh simply lets the sliding TTL lapse), so it must neither throw on a
        /// transient Redis fault — which would tear down the live read loop — nor add a serial round-trip to
        /// the front of every inbound command.
        /// </summary>
        private void RefreshSocketPresence(int playerId, string socketId)
        {
            _cache.ReclaimAndForget(CurrentSocketKey(playerId), socketId, SocketPresenceTtl);
        }

        public Task UnRegisterSocket(SocketContext context)
        {
            // A clean disconnect shares the same guarded best-effort steps as a registration rollback: if the
            // unsubscribe throws, the presence-key release must still run so the key can't survive its full
            // TTL reporting a ghost session that makes HasActiveSocket lie until it expires.
            return TeardownSocketRegistration(context, "tearing down the socket");
        }

        public async Task EmitSocketCommand(SocketCommandInfo commandInfo, string socketId)
        {
            await _pubSub.Publish(SocketChannel(socketId), SocketQueueName(socketId), commandInfo);
            // Best-effort backstop (see SocketQueueTtl): the queue key normally drains in milliseconds and is
            // deleted outright on graceful teardown, so a missed refresh here just means the next push retries it.
            _cache.ExpireAndForget(SocketQueueName(socketId), SocketQueueTtl);
        }

        /// <summary>
        /// Publishes to whatever socket is currently live for <paramref name="playerId"/>, returning whether
        /// one was found to publish to (not whether the client actually received it — the push itself is
        /// fire-and-forget). The Ops dead-letter replay (#1542) uses this to gate its queue removal on a
        /// single presence lookup rather than a separate check-then-act pair, so a resolved switch-credit
        /// claim (see <see cref="TryClaimForSwitchCredit"/>) must report exactly like "no active socket" — it
        /// isn't a socket to publish to, and reporting it as one would let the caller believe a push was
        /// delivered when it was actually published into a queue nothing drains.
        /// </summary>
        public async Task<bool> EmitSocketCommand(SocketCommandInfo commandInfo, int playerId)
        {
            var socketId = await CurrentSocketId(playerId);
            if (socketId is not null && socketId != SwitchCreditClaimValue)
            {
                await EmitSocketCommand(commandInfo, socketId);
                return true;
            }
            else
            {
                _logger.LogWarning("Attempted to emit command: {CommandInfo} to player with no active socket: {PlayerId}", commandInfo, playerId);
                return false;
            }
        }

        private async Task UnRegisterSocketCommandListener(string socketId)
        {
            await _pubSub.UnSubscribe(socketId);
        }

        private async Task RegisterSocketCommandListener(SocketHandler handler)
        {
            var channel = SocketChannel(handler.Id);
            var queueName = SocketQueueName(handler.Id);
            var processor = GetSocketCommandProcessor(handler);
            await _pubSub.Subscribe(channel, queueName, async args => await processor(args.queue), handler.Id);
        }

        /// <summary>
        /// The number of consecutive dequeue failures after which the command processor abandons its drain
        /// loop rather than hot-spinning. A sustained fault (Redis unreachable, or a throw before the pop)
        /// would otherwise tight-loop hammering Redis and the log for the life of the connection; the socket
        /// re-establishes its subscription on reconnect.
        /// </summary>
        internal const int MaxConsecutiveDequeueFailures = 5;

        /// <summary>Base backoff between consecutive dequeue failures, scaled by the failure streak.</summary>
        private static readonly TimeSpan DequeueFailureBackoff = TimeSpan.FromMilliseconds(100);

        private Func<IPubSubQueue, Task> GetSocketCommandProcessor(SocketHandler socket)
        {
            return async (queue) =>
            {
                var consecutiveFailures = 0;
                while (true)
                {
                    SocketCommandInfo? nextCommandInfo;
                    try
                    {
                        nextCommandInfo = await queue.GetNextAsync<SocketCommandInfo>();
                    }
                    catch (Exception ex)
                    {
                        // A fault dequeuing the next message (a malformed payload or a Redis blip) must not
                        // escape the processor and kill the drain loop — that would silently drop every later
                        // server push for this socket (#691). A malformed payload is already popped by
                        // GetNextAsync before deserialization throws, so skipping it advances the queue; a
                        // transient blip is retried on the next pass.
                        _logger.LogError(ex, "An error occurred while dequeuing a socket command on socket: {Id}, playerId: {PlayerId}.", socket.Id, socket.PlayerId);

                        // Bound a persistent fault: a hot retry loop would hammer Redis and the log for the
                        // life of the connection. Back off (scaled by the streak) and give up past a ceiling —
                        // the socket re-establishes its subscription on reconnect.
                        if (++consecutiveFailures >= MaxConsecutiveDequeueFailures)
                        {
                            _logger.LogError("Abandoning the command processor for socket: {Id}, playerId: {PlayerId} after {FailureCount} consecutive dequeue failures; it will re-establish on reconnect.", socket.Id, socket.PlayerId, consecutiveFailures);
                            break;
                        }

                        await Task.Delay(DequeueFailureBackoff * consecutiveFailures);
                        continue;
                    }

                    consecutiveFailures = 0;

                    if (nextCommandInfo is null)
                    {
                        break;
                    }

                    _logger.LogTrace("Received command on socket: {Id}, playerId: {PlayerId}, command: {CommandInfo}.", socket.Id, socket.PlayerId, nextCommandInfo);

                    // ExecuteServerCommand contains every fault and reports the outcome (it never throws), so a
                    // genuine fault no longer silently drops the push: escalate it (dead-letter + client
                    // re-sync notice) while the queue keeps draining. A malformed server push is a genuine bug
                    // (the payload is server-authored) and escalates the same as a fault. A teardown
                    // cancellation, a timeout, and a failed delivery (the command ran; only the send failed) are
                    // not poisoned payloads and need no escalation.
                    var outcome = await socket.ExecuteServerCommand(nextCommandInfo);
                    if (outcome is SocketCommandOutcome.Faulted or SocketCommandOutcome.MalformedParameters)
                    {
                        await EscalateFailedServerCommand(socket, nextCommandInfo);
                    }
                }
            };
        }

        /// <summary>
        /// Escalates a persistently-failing server-initiated command: dead-letters the poisoned payload so it
        /// is preserved for inspection/replay rather than silently dropped, then pushes a
        /// <see cref="ServerCommandFailed"/> notice to the affected socket so the client re-syncs the
        /// authoritative state the failed push would have updated instead of silently diverging (#671). The
        /// dead-lettered payload carries the player id it was addressed to (<see cref="SocketCommandDeadLetterEnvelope"/>)
        /// so the Ops replay surface (#1542) can redeliver it to whatever socket is currently live for that
        /// player — the depth is logged on every escalation (rather than only on growth, since an escalation
        /// is already rare) so an accumulating backlog is at least visible to alerting.
        /// </summary>
        private async Task EscalateFailedServerCommand(SocketHandler socket, SocketCommandInfo commandInfo)
        {
            long? deadLetterDepth = null;
            try
            {
                var deadLetterQueue = _pubSub.GetQueue(Constants.PUBSUB_SOCKET_DEAD_LETTER_QUEUE);
                var envelope = new SocketCommandDeadLetterEnvelope { PlayerId = socket.PlayerId, Command = commandInfo };
                await deadLetterQueue.AddToQueueAsync(envelope.Serialize());
                deadLetterDepth = await deadLetterQueue.GetLengthAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dead-letter a faulted server-initiated command: {CommandInfo} on socket: {Id}", commandInfo, socket.Id);
            }

            // The notice is itself a server command, so don't surface a failed notice with another notice —
            // that would risk an escalation loop. The dead-letter above already preserves it.
            if (commandInfo.Name != nameof(ServerCommandFailed))
            {
                // Dispatched directly (not re-queued over pub/sub) since the failing socket is right here;
                // ExecuteServerCommand runs it under the same command lock and owns the send to the client.
                // Capture the outcome: if the notice itself doesn't get through (e.g. the socket just closed),
                // the client never gets the re-sync cue for a gating command like ChallengeCompleted and would
                // diverge silently — so surface it for an operator rather than ignoring the result.
                var noticeOutcome = await socket.ExecuteServerCommand(new ServerCommandFailedInfo(commandInfo.Name));
                if (noticeOutcome is not SocketCommandOutcome.Succeeded)
                {
                    _logger.LogWarning("The re-sync notice for a failed server command did not complete (outcome: {Outcome}); the client may not have received the re-sync cue for {Command} on socket: {Id}.", noticeOutcome, commandInfo.Name, socket.Id);
                }
            }

            _logger.LogWarning("Dead-lettered a failing server-initiated command and notified the client: {CommandInfo} on socket: {Id}. Dead-letter queue depth: {Depth}", commandInfo, socket.Id, deadLetterDepth);
        }

        private async Task<string?> CurrentSocketId(int playerId)
        {
            return await _cache.Get(CurrentSocketKey(playerId));
        }

        private static string SocketQueueName(string socketId)
        {
            return $"{Constants.PUBSUB_SOCKET_QUEUE_PREFIX}_{socketId}";
        }

        private static string SocketChannel(string socketId)
        {
            return $"{Constants.PUBSUB_SOCKET_CHANNEL_PREFIX}_{socketId}";
        }

        private static string CurrentSocketKey(int playerId)
        {
            return $"{Constants.CACHE_PLAYER_SOCKET_PREFIX}_{playerId}";
        }
    }
}
