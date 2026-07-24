using Game.Abstractions.Infrastructure;
using Game.Api.Sockets;
using Game.Api.Sockets.Commands;
using Game.Core;
using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;

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
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Time-to-live on the per-player socket-presence key (and the account-level "current live player"
        /// key it shares its shape with, see <see cref="ClaimAccountPresence"/>). The client heartbeats
        /// every 10s (<c>api-socket.ts</c>), and every inbound message refreshes this TTL, so a live
        /// connection keeps its key fresh while a non-gracefully-closed one (a tab/device that vanished
        /// without a close frame) eventually lets the key expire.
        /// </summary>
        /// <remarks>
        /// Pinned to <em>at least</em> <see cref="SocketHandler.DefaultInactivityTimeout"/> +
        /// <see cref="SocketHandler.DefaultInactivityPollInterval"/> — the watchdog's own worst-case bound
        /// on how long a silent socket may still be genuinely alive — rather than a shorter, UI-responsive
        /// value (#1817). <see cref="TryClaimForSwitchCredit"/> and <see cref="ClaimAccountPresence"/> both
        /// treat an expired presence key as "safe to claim"; if the TTL were shorter than the watchdog's
        /// bound, a backgrounded-but-still-open socket (throttled well past 10s between heartbeats — see
        /// spike #922's background-tab-throttling note) could lose its presence claim to a switch-credit or
        /// an account-level takeover while the watchdog still considers it live and lets it keep processing
        /// commands, reintroducing the lost-update race those claims exist to prevent. The trade-off is a
        /// slower <see cref="HasActiveSocket"/> signal for a socket that vanished uncleanly (no close frame):
        /// it now takes up to this TTL, not a shorter one, to be reported gone.
        /// </remarks>
        internal static readonly TimeSpan SocketPresenceTtl = SocketHandler.DefaultInactivityTimeout + SocketHandler.DefaultInactivityPollInterval;

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
        /// Sentinel prefix <see cref="TryClaimForSwitchCredit"/> writes into a presence key, followed by a
        /// unique suffix per claim (rather than one shared literal): not a real socket id, so
        /// <see cref="RegisterSocket"/> must recognize it (rather than notifying it as a replaced connection)
        /// and <see cref="CurrentSocketId"/>'s callers must never treat it as a live socket. The per-claim
        /// uniqueness lets <see cref="ClaimPresenceKey"/> tell a still-in-flight claim it's already waiting on
        /// apart from a newer one that replaced it (#2374) — a shared literal made every claim indistinguishable,
        /// so a registration that had been waiting a while could CAS over a seconds-old claim while its
        /// read-modify-write was still in flight.
        /// </summary>
        private const string SwitchCreditClaimPrefix = "switch-credit:";

        /// <summary>
        /// TTL on a switch-away credit's claim (see <see cref="TryClaimForSwitchCredit"/>). Generous relative to
        /// the credit's actual work (a player load plus an offline-progress simulation bounded well under the
        /// socket command timeout) so it only bites a crashed credit that never released its claim, bounding how
        /// long <see cref="RegisterSocket"/> will ever defer behind one.
        /// </summary>
        private static readonly TimeSpan SwitchCreditClaimTtl = TimeSpan.FromSeconds(20);

        /// <summary>Poll interval <see cref="RegisterSocket"/> uses while waiting out a switch-credit claim.</summary>
        private static readonly TimeSpan SwitchCreditWaitPollInterval = TimeSpan.FromMilliseconds(50);

        public SocketManagerService(IPubSubService pubSub, ICacheService cache, SocketCommandFactory commandFactory, IServiceScopeFactory scopeFactory, ILoggerFactory loggerFactory, SocketConnectionRegistry socketRegistry, TimeProvider? timeProvider = null)
        {
            _pubSub = pubSub;
            _cache = cache;
            _scopeFactory = scopeFactory;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<SocketManagerService>();
            _commandFactory = commandFactory;
            _socketRegistry = socketRegistry;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public async Task<SocketContext> RegisterSocket(WebSocket socket, SessionService sessionService, bool isAdmin)
        {
            var userId = sessionService.UserId;
            var playerId = sessionService.SelectedPlayerId;
            var socketContext = new SocketContext(socket, playerId, sessionService, isAdmin, _loggerFactory.CreateLogger<SocketContext>());
            var socketHandler = new SocketHandler(socketContext, _commandFactory, _scopeFactory, _loggerFactory.CreateLogger<SocketHandler>(), () => RefreshSocketPresence(userId, playerId, socketContext.SocketId));
            var presenceKey = CurrentSocketKey(playerId);
            // Defer behind an in-flight switch-away credit's claim (#2041) rather than overwriting it: that
            // credit is a read-modify-write against this same player's aggregate run off this player's battle
            // loop, so registering (and starting the loop) while it's still mid-flight would reintroduce the
            // lost-update race the presence claim exists to prevent. ClaimPresenceKey's CompareAndSet loop
            // closes the gap a separate wait-then-unconditional-write would leave between "the claim looked
            // clear" and "the key was written" — see its doc comment.
            var oldSocketId = await ClaimPresenceKey(presenceKey, socketContext.SocketId);

            int? oldAccountPlayerId;
            try
            {
                // One live character per account (spike #922, decided A++) is enforced here too, not just one
                // live socket per character: Session_{userId} (SessionStore) is keyed by account, so if a
                // *different* character on this account still had a live socket, its connection would keep
                // saving its own in-flight battle into the same cache slot this connection is about to use,
                // last-writer-wins, with neither side ever told (#1817). Claiming the account-level slot
                // mirrors ClaimPresenceKey exactly, just keyed by userId instead of playerId. Inside the try
                // (rather than before it) so a fault here rolls the per-player claim back too, instead of
                // leaving it briefly orphaned until its own TTL.
                oldAccountPlayerId = await ClaimAccountPresence(userId, playerId, socketContext.SocketId);

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
                await NotifyReplacedSocket(oldSocketId, playerId);
            }

            if (oldAccountPlayerId is int otherPlayerId)
            {
                // The other character's own presence key (not the account key just claimed above) names its
                // live socket, if any. A well-behaved switch already closed it — CharacterSelectionService's
                // switch-away credit runs only after the client tears its game socket down — so this is
                // normally a no-op and only fires for a client that opened a second character's socket
                // without doing so (a stale/second tab, or a misbehaving client).
                var otherSocketId = await CurrentSocketId(otherPlayerId);
                if (otherSocketId is not null && !IsSwitchCreditClaim(otherSocketId))
                {
                    await NotifyReplacedSocket(otherSocketId, otherPlayerId);
                }
            }

            _logger.LogDebug("Initiated socket for player: ({Id}), with Id: {SocketId}", playerId, socketContext.SocketId);
            return socketContext;
        }

        /// <summary>Best-effort push telling a superseded socket it was replaced; see the callers in <see cref="RegisterSocket"/>.</summary>
        private async Task NotifyReplacedSocket(string oldSocketId, int oldPlayerId)
        {
            try
            {
                await EmitSocketCommand(new SocketReplacedInfo(), oldSocketId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify replaced socket {OldSocketId} for player {PlayerId}; it will be cleaned up by its own inactivity teardown.", oldSocketId, oldPlayerId);
            }
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

            try
            {
                // Same compare-and-delete guard as the per-player key above, keyed by account instead: a
                // different character on this account (or a newer socket for this *same* character, #2235 —
                // see AccountPresenceValue) may have already taken the account-level slot over, and that
                // claim must be left intact.
                await _cache.CompareAndDelete(AccountSocketKey(context.Session.UserId), AccountPresenceValue(context.PlayerId, context.SocketId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release the account presence key for socket {SocketId} while {Reason}.", context.SocketId, reason);
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
        /// (<c>CharacterSelectionService.CreditDepartedCharacter</c>, #2041), replacing a plain presence read: the read
        /// and the claim happen in one Redis round trip, so there is no gap between "no socket is here" and
        /// "reserve this slot" for a concurrent <see cref="RegisterSocket"/> to land in. Returns the claim value
        /// to pass back to <see cref="ReleaseSwitchCreditClaim"/> on success, or <see langword="null"/> when the
        /// key is already held — a genuinely live socket (the credit must be skipped, its battle loop owns the
        /// saves) or another switch-credit already claiming it.
        /// </summary>
        public async Task<string?> TryClaimForSwitchCredit(int playerId)
        {
            var claimValue = SwitchCreditClaimPrefix + Guid.NewGuid().ToString("N");
            return await _cache.CompareAndSet(CurrentSocketKey(playerId), null, claimValue, SwitchCreditClaimTtl) ? claimValue : null;
        }

        /// <summary>
        /// Releases a switch-away credit's claim taken by <see cref="TryClaimForSwitchCredit"/>, given the exact
        /// <paramref name="claimValue"/> it returned. Compare-and-delete so it only clears the key while the
        /// claim is still ours — a concurrent <see cref="RegisterSocket"/> may have already kicked it and
        /// claimed the key for a real socket, or a newer switch-credit claim may have landed after this one's
        /// own TTL lapsed, either of which must be left intact.
        /// </summary>
        public async Task ReleaseSwitchCreditClaim(int playerId, string claimValue)
        {
            await _cache.CompareAndDelete(CurrentSocketKey(playerId), claimValue);
        }

        /// <summary>Whether <paramref name="value"/> is a switch-credit claim rather than a real socket id.</summary>
        private static bool IsSwitchCreditClaim(string? value)
        {
            return value is not null && value.StartsWith(SwitchCreditClaimPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Atomically claims <paramref name="presenceKey"/> as <paramref name="newSocketId"/> via a
        /// CompareAndSet loop, so a switch-away credit's claim (#2041) landing in the gap between reading the
        /// key and writing it can never be kicked: the write only lands if the key's value is still what was
        /// just read, so a claim (or another registration) that lands first makes the CompareAndSet fail, and
        /// the loop re-reads and retries against whatever is there now instead of blindly overwriting it — the
        /// gap a separate wait-then-unconditional-write would leave open. While the read value is a switch-credit
        /// claim, the loop defers (polling at <see cref="SwitchCreditWaitPollInterval"/>) rather than racing it,
        /// against a deadline that restarts every time the observed claim's value changes (#2374) — each claim
        /// carries a unique id (see <see cref="SwitchCreditClaimPrefix"/>), so a fresh claim that replaced the one
        /// we started waiting on gets its own full <see cref="SwitchCreditClaimTtl"/> window rather than
        /// inheriting whatever was left of the original wait, which could otherwise let this loop CAS over a
        /// seconds-old claim while its read-modify-write is still in flight. A claim that faults without
        /// releasing still can't wedge a registration past one TTL window once no newer claim replaces it; only
        /// then does an attempt targeting the (by then stale) claim proceed. Returns the previous real socket id,
        /// or <see langword="null"/> if the key was unset or held only a switch-credit claim.
        /// </summary>
        private async Task<string?> ClaimPresenceKey(string presenceKey, string newSocketId)
        {
            var currentValue = await _cache.Get(presenceKey);
            var observedClaim = IsSwitchCreditClaim(currentValue) ? currentValue : null;
            var deadline = _timeProvider.GetUtcNow() + SwitchCreditClaimTtl;
            while (true)
            {
                if (IsSwitchCreditClaim(currentValue) && _timeProvider.GetUtcNow() < deadline)
                {
                    await Task.Delay(SwitchCreditWaitPollInterval);
                    currentValue = await _cache.Get(presenceKey);
                    if (IsSwitchCreditClaim(currentValue) && currentValue != observedClaim)
                    {
                        observedClaim = currentValue;
                        deadline = _timeProvider.GetUtcNow() + SwitchCreditClaimTtl;
                    }

                    continue;
                }

                if (await _cache.CompareAndSet(presenceKey, currentValue, newSocketId, SocketPresenceTtl))
                {
                    return IsSwitchCreditClaim(currentValue) ? null : currentValue;
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
        /// Atomically claims <paramref name="userId"/>'s account-level "current live character" slot for
        /// <paramref name="playerId"/>'s <paramref name="socketId"/> — the same CompareAndSet-loop shape as
        /// <see cref="ClaimPresenceKey"/>, just keyed by account instead of by character, and with no
        /// switch-credit sentinel to defer behind (that claim only ever targets a per-player key). Enforces
        /// the decided one-live-character-per-account model (spike #922) at the socket layer, alongside the
        /// per-player takeover <see cref="ClaimPresenceKey"/> already enforces (#1817). Returns the slot's
        /// previous player id when it names a genuinely different character to kick, or <see langword="null"/>
        /// if the slot was unset or already held this same player (a same-character reconnect, where the
        /// stored value's socket id differs but its player id does not — see <see cref="AccountPresenceValue"/>).
        /// </summary>
        private async Task<int?> ClaimAccountPresence(int userId, int playerId, string socketId)
        {
            var accountKey = AccountSocketKey(userId);
            var newValue = AccountPresenceValue(playerId, socketId);
            var currentValue = await _cache.Get(accountKey);
            while (true)
            {
                if (await _cache.CompareAndSet(accountKey, currentValue, newValue, SocketPresenceTtl))
                {
                    var previousPlayerId = currentValue is not null ? ParseAccountPresencePlayerId(currentValue) : (int?)null;
                    return previousPlayerId is int prev && prev != playerId ? prev : null;
                }

                // Another writer (a competing registration for this account) won the race; re-read and retry
                // against whatever landed, exactly as ClaimPresenceKey does.
                currentValue = await _cache.Get(accountKey);
            }
        }

        /// <summary>
        /// Extends the player's socket-presence TTL — and the account-level slot claiming it as the
        /// account's current live character — on connection activity, so a live socket keeps both from
        /// expiring (see <see cref="SocketPresenceTtl"/>). Both use <see cref="ICacheService.ReclaimAndForget"/>:
        /// each key is resurrected as this socket's/player's own claim if currently unset (a TTL lapse from
        /// an inbound stall, or a rolled-back registration), but if a key already holds a different (newer)
        /// owner's value the refresh only extends that key's TTL without touching it, so it can never clobber
        /// a takeover. Fire-and-forget: presence refresh is best-effort (a missed refresh simply lets the
        /// sliding TTL lapse), so it must neither throw on a transient Redis fault — which would tear down the
        /// live read loop — nor add a serial round-trip to the front of every inbound command.
        /// </summary>
        private void RefreshSocketPresence(int userId, int playerId, string socketId)
        {
            _cache.ReclaimAndForget(CurrentSocketKey(playerId), socketId, SocketPresenceTtl);
            _cache.ReclaimAndForget(AccountSocketKey(userId), AccountPresenceValue(playerId, socketId), SocketPresenceTtl);
        }

        /// <summary>
        /// Force-closes any currently live socket among <paramref name="playerIds"/> — an account's full
        /// player list — so an admin ban/archive revokes access immediately rather than only once the
        /// client's access token expires (up to the 15-minute TTL) or the socket next goes idle. Silent when
        /// none are connected: unlike a gameplay push (<see cref="EmitSocketCommand(SocketCommandInfo, int)"/>),
        /// the target of a ban/archive is very often already offline, so that is the common case here, not a
        /// warning-worthy anomaly. Reads the presence key directly (mirroring the <c>otherSocketId</c> check
        /// in <see cref="RegisterSocket"/>) and skips a null or switch-credit-sentinel value, rather than
        /// gating on <see cref="HasActiveSocket"/> and then calling the int-keyed <see cref="EmitSocketCommand(SocketCommandInfo, int)"/>
        /// overload — that combination re-reads the key and, during the brief window a switch-credit claim
        /// holds it (see <see cref="TryClaimForSwitchCredit"/>), finds no real socket and logs the very
        /// no-active-socket warning this method exists to suppress (#2233). Best-effort like the rest of this
        /// service's presence handling: a player who connects in the narrow race between the admin action
        /// committing and this call reaching their socket is not caught, which is acceptable since any
        /// subsequent refresh/select for the account is already rejected by the live ban/archive check (see
        /// docs/backend-auth.md).
        /// </summary>
        public async Task RevokeAccess(IReadOnlyList<int> playerIds)
        {
            foreach (var playerId in playerIds)
            {
                var socketId = await CurrentSocketId(playerId);
                if (socketId is not null && !IsSwitchCreditClaim(socketId))
                {
                    await EmitSocketCommand(new AccessRevokedInfo(), socketId);
                }
            }
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
            if (socketId is not null && !IsSwitchCreditClaim(socketId))
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
                    string? rawMessage;
                    try
                    {
                        // Popped as the raw string (rather than queue.GetNextAsync<SocketCommandInfo>()) so a
                        // payload that fails to deserialize is still available to dead-letter below — the
                        // popped-before-deserialized item can't otherwise be recovered once GetNextAsync<T>
                        // throws (#2272).
                        rawMessage = await queue.GetNextAsync();
                    }
                    catch (Exception ex)
                    {
                        // A fault popping the next message (a Redis blip) must not escape the processor and
                        // kill the drain loop — that would silently drop every later server push for this
                        // socket (#691). Retried on the next pass.
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

                    if (rawMessage is null)
                    {
                        break;
                    }

                    SocketCommandInfo? nextCommandInfo;
                    try
                    {
                        nextCommandInfo = rawMessage.Deserialize<SocketCommandInfo>();
                    }
                    catch (JsonException ex)
                    {
                        // An envelope-level break (most plausibly wire-shape skew during a rolling deploy) is a
                        // genuine bug, since the payload is server-authored — escalate it the same as a fault
                        // (dead-letter + client re-sync notice) rather than only logging it, mirroring the
                        // player write-behind queue's malformed-envelope handling. The pop above already
                        // advanced the queue, so this is purely an escalation add.
                        _logger.LogError(ex, "Dead-lettering a socket command with a malformed envelope on socket: {Id}, playerId: {PlayerId}. Raw message: {RawMessage}", socket.Id, socket.PlayerId, rawMessage);
                        await EscalateMalformedServerCommand(socket, rawMessage);
                        continue;
                    }

                    if (nextCommandInfo is null)
                    {
                        // A null payload deserialized cleanly but carries no command; the same poison-payload
                        // treatment as a JSON parse failure applies.
                        _logger.LogError("Dead-lettering an empty socket command envelope on socket: {Id}, playerId: {PlayerId}. Raw message: {RawMessage}", socket.Id, socket.PlayerId, rawMessage);
                        await EscalateMalformedServerCommand(socket, rawMessage);
                        continue;
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
                await SendServerCommandFailedNotice(socket, commandInfo.Name);
            }

            _logger.LogWarning("Dead-lettered a failing server-initiated command and notified the client: {CommandInfo} on socket: {Id}. Dead-letter queue depth: {Depth}", commandInfo, socket.Id, deadLetterDepth);
        }

        /// <summary>
        /// Escalates a server push whose envelope itself failed to deserialize — the payload is already popped
        /// off the queue by the time <see cref="GetSocketCommandProcessor"/> discovers it's malformed, so
        /// unlike <see cref="EscalateFailedServerCommand"/> there is no parsed <see cref="SocketCommandInfo"/>
        /// to wrap in a <see cref="SocketCommandDeadLetterEnvelope"/> (and no failed command name to report).
        /// The raw string is dead-lettered as-is: the Ops inspector's classifier already treats an entry that
        /// doesn't parse as a <see cref="SocketCommandDeadLetterEnvelope"/> as malformed, so this needs no
        /// reader-side change. The client still gets the re-sync notice — with a sentinel command name, since
        /// which specific push it missed can no longer be recovered — mirroring the player write-behind
        /// queue's malformed-envelope handling (#2272).
        /// </summary>
        private async Task EscalateMalformedServerCommand(SocketHandler socket, string rawMessage)
        {
            long? deadLetterDepth = null;
            try
            {
                var deadLetterQueue = _pubSub.GetQueue(Constants.PUBSUB_SOCKET_DEAD_LETTER_QUEUE);
                await deadLetterQueue.AddToQueueAsync(rawMessage);
                deadLetterDepth = await deadLetterQueue.GetLengthAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dead-letter a socket command with a malformed envelope on socket: {Id}. Raw message: {RawMessage}", socket.Id, rawMessage);
            }

            await SendServerCommandFailedNotice(socket, UnknownServerCommandName);

            _logger.LogWarning("Dead-lettered a socket command with a malformed envelope and notified the client on socket: {Id}. Raw message: {RawMessage}. Dead-letter queue depth: {Depth}", socket.Id, rawMessage, deadLetterDepth);
        }

        /// <summary>
        /// Dispatched directly (not re-queued over pub/sub) since the failing socket is right here;
        /// ExecuteServerCommand runs it under the same command lock and owns the send to the client. Capture
        /// the outcome: if the notice itself doesn't get through (e.g. the socket just closed), the client
        /// never gets the re-sync cue for a gating command like ChallengeCompleted and would diverge silently
        /// — so surface it for an operator rather than ignoring the result.
        /// </summary>
        private async Task SendServerCommandFailedNotice(SocketHandler socket, string failedCommandName)
        {
            var noticeOutcome = await socket.ExecuteServerCommand(new ServerCommandFailedInfo(failedCommandName));
            if (noticeOutcome is not SocketCommandOutcome.Succeeded)
            {
                _logger.LogWarning("The re-sync notice for a failed server command did not complete (outcome: {Outcome}); the client may not have received the re-sync cue for {Command} on socket: {Id}.", noticeOutcome, failedCommandName, socket.Id);
            }
        }

        /// <summary>
        /// Sentinel <see cref="ServerCommandFailedModel.CommandName"/> for <see cref="EscalateMalformedServerCommand"/>:
        /// an envelope-level parse failure never resolves a command name, but the model requires one. The
        /// frontend's <c>handleServerCommandFailed</c> only special-cases known command names and otherwise
        /// just logs the notice, so a sentinel that matches nothing is a safe, self-describing placeholder.
        /// </summary>
        private const string UnknownServerCommandName = "Unknown";

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

        /// <summary>The account-level "current live character" key <see cref="ClaimAccountPresence"/> claims.</summary>
        private static string AccountSocketKey(int userId)
        {
            return $"{Constants.CACHE_ACCOUNT_SOCKET_PREFIX}_{userId}";
        }

        private static string PlayerIdValue(int playerId) => playerId.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// The value stored at an account-level presence key (<see cref="AccountSocketKey"/>): the live
        /// character's player id plus the claiming socket's id, rather than the player id alone. A same-
        /// character reconnect writes a new socket id for the same player id, so the old socket's teardown
        /// (a compare-and-delete against its own claim, see <see cref="TeardownSocketRegistration"/>) can no
        /// longer match — and therefore can no longer delete — the new socket's still-live claim (#2235).
        /// </summary>
        private static string AccountPresenceValue(int playerId, string socketId) => $"{PlayerIdValue(playerId)}:{socketId}";

        /// <summary>Extracts the player id embedded in an <see cref="AccountPresenceValue"/>.</summary>
        private static int ParseAccountPresencePlayerId(string value) => int.Parse(value.Split(':', 2)[0], CultureInfo.InvariantCulture);
    }
}
