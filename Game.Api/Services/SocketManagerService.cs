using Game.Abstractions.Infrastructure;
using Game.Api.Sockets;
using Game.Api.Sockets.Commands;
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
            var oldSocketId = await _cache.GetSet(presenceKey, socketContext.SocketId);
            await _cache.Expire(presenceKey, SocketPresenceTtl);
            if (oldSocketId is not null)
            {
                await EmitSocketCommand(new SocketReplacedInfo(), oldSocketId);
            }

            await RegisterSocketCommandListener(socketHandler);
            // Register before starting the loops so the registry tracks the socket — and threads its
            // shutdown tokens into Listen — for a graceful drain on host shutdown (#526).
            _socketRegistry.Register(socketHandler);
            _logger.LogDebug("Initiated socket for player: ({Id}), with Id: {SocketId}", playerId, socketContext.SocketId);
            return socketContext;
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
                var nextCommandInfo = await queue.GetNextAsync<SocketCommandInfo>();
                while (nextCommandInfo is not null)
                {
                    try
                    {
                        _logger.LogTrace("Received command on socket: {Id}, playerId: {PlayerId}, command: {CommandInfo}.", socket.Id, socket.PlayerId, nextCommandInfo);
                        await socket.ExecuteCommand(nextCommandInfo);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An error occured while executing a socket command: {CommandInfo}", nextCommandInfo);
                    }

                    nextCommandInfo = await queue.GetNextAsync<SocketCommandInfo>();
                }
            };
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
