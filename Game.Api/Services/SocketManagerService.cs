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
                if (oldSocketId is not null)
                {
                    await EmitSocketCommand(new SocketReplacedInfo(), oldSocketId);
                }

                await RegisterSocketCommandListener(socketHandler);
                // Register before starting the loops so the registry tracks the socket — and threads its
                // shutdown tokens into Listen — for a graceful drain on host shutdown (#526).
                _socketRegistry.Register(socketHandler);
            }
            catch
            {
                // A step after the presence-key write failed, so the key now points at a socket whose drain
                // loops never started — a "registered but dead" presence that would block the player and never
                // drain. Undo the partial registration before propagating.
                await RollbackRegistration(socketContext);
                throw;
            }

            _logger.LogDebug("Initiated socket for player: ({Id}), with Id: {SocketId}", playerId, socketContext.SocketId);
            return socketContext;
        }

        /// <summary>
        /// Undoes a partially-completed <see cref="RegisterSocket"/> after the presence key was written but a
        /// later step threw: drop our presence claim (only if it is still ours) and tear down any subscription
        /// and registry tracking. Each step is best-effort and guarded so a cleanup fault can't mask the
        /// original registration exception that is about to propagate.
        /// </summary>
        private async Task RollbackRegistration(SocketContext context)
        {
            _socketRegistry.Unregister(context.SocketId);
            try
            {
                await UnRegisterSocketCommandListener(context.SocketId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unsubscribe socket {SocketId} while rolling back a failed registration.", context.SocketId);
            }

            try
            {
                // Compare-and-delete so we only release the key while it is still ours — a newer connection may
                // have taken it over between our write and this rollback, and that key must be left intact.
                await _cache.CompareAndDelete(CurrentSocketKey(context.PlayerId), context.SocketId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release the presence key for socket {SocketId} while rolling back a failed registration.", context.SocketId);
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

        public async Task UnRegisterSocket(SocketContext context)
        {
            _socketRegistry.Unregister(context.SocketId);
            await UnRegisterSocketCommandListener(context.SocketId);
            await _cache.CompareAndDelete(CurrentSocketKey(context.PlayerId), context.SocketId);
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

        private Func<IPubSubQueue, Task> GetSocketCommandProcessor(SocketHandler socket)
        {
            return async (queue) =>
            {
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
                        continue;
                    }

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
                await socket.ExecuteServerCommand(new ServerCommandFailedInfo(commandInfo.Name));
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
