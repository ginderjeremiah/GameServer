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

        public async Task<SocketContext> RegisterSocket(WebSocket socket, SessionService sessionService)
        {
            var playerId = sessionService.SelectedPlayerId;
            var socketContext = new SocketContext(socket, playerId, sessionService, _loggerFactory.CreateLogger<SocketContext>());
            var socketHandler = new SocketHandler(socketContext, _commandFactory, _scopeFactory, _loggerFactory.CreateLogger<SocketHandler>(), () => RefreshSocketPresence(playerId));
            var presenceKey = CurrentSocketKey(playerId);
            // Claim the presence key with its TTL atomically so a fault here can never leave the key without an
            // expiry — a TTL-less key would defeat the heartbeat design and report a permanent ghost session.
            var oldSocketId = await _cache.GetSet(presenceKey, socketContext.SocketId, SocketPresenceTtl);
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
        /// an existing session.
        /// </summary>
        public async Task<bool> HasActiveSocket(int playerId)
        {
            return await CurrentSocketId(playerId) is not null;
        }

        /// <summary>
        /// Extends the player's socket-presence TTL on connection activity, so a live socket keeps its
        /// presence key from expiring (see <see cref="SocketPresenceTtl"/>). Uses an expire (not a
        /// re-set) so it only ever prolongs the currently-registered socket's key and never resurrects a
        /// key a newer connection has since taken over.
        /// </summary>
        private async Task RefreshSocketPresence(int playerId)
        {
            await _cache.Expire(CurrentSocketKey(playerId), SocketPresenceTtl);
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
        }

        public async Task EmitSocketCommand(SocketCommandInfo commandInfo, int playerId)
        {
            var socketId = await CurrentSocketId(playerId);
            if (socketId is not null)
            {
                await _pubSub.Publish(SocketChannel(socketId), SocketQueueName(socketId), commandInfo);
            }
            else
            {
                _logger.LogWarning("Attempted to emit command: {CommandInfo} to player with no active socket: {PlayerId}", commandInfo, playerId);
            }
        }

        private async Task UnRegisterSocketCommandListener(string socketId)
        {
            await _pubSub.UnSubscribe(SocketChannel(socketId), socketId);
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
                    // re-sync notice) while the queue keeps draining. A teardown cancellation and a timeout are
                    // not command defects and need no escalation.
                    var outcome = await socket.ExecuteServerCommand(nextCommandInfo);
                    if (outcome is SocketCommandOutcome.Faulted)
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
        /// authoritative state the failed push would have updated instead of silently diverging (#671).
        /// </summary>
        private async Task EscalateFailedServerCommand(SocketHandler socket, SocketCommandInfo commandInfo)
        {
            try
            {
                var deadLetterQueue = _pubSub.GetQueue(Constants.PUBSUB_SOCKET_DEAD_LETTER_QUEUE);
                await deadLetterQueue.AddToQueueAsync(commandInfo.Serialize());
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

            _logger.LogWarning("Dead-lettered a failing server-initiated command and notified the client: {CommandInfo} on socket: {Id}", commandInfo, socket.Id);
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
