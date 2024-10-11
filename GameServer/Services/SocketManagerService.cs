using GameCore;
using GameCore.Infrastructure;
using GameCore.Sessions;
using GameInfrastructure;
using GameServer.Sockets;
using GameServer.Sockets.Commands;
using System.Net.WebSockets;

namespace GameServer.Services
{
    public class SocketManagerService
    {
        private readonly IPubSubService _pubSub;
        private readonly ICacheService _cache;
        private readonly IApiLogger _logger;
        private readonly SocketCommandFactory _commandFactory;

        public SocketManagerService(IDataServicesFactory dataServices, SocketCommandFactory commandFactory)
        {
            _pubSub = dataServices.PubSub;
            _cache = dataServices.Cache;
            _logger = dataServices.Logger;
            _commandFactory = commandFactory;
        }

        public async Task<SocketContext> RegisterSocket(WebSocket socket, SessionPlayer player)
        {
            var socketContext = new SocketContext(socket, player.Id);
            var socketHandler = new SocketHandler(socketContext, _commandFactory, _logger);
            var oldSocketId = await _cache.GetSetAsync(CurrentSocketKey(player.Id), socketContext.SocketId);
            if (oldSocketId is not null)
            {
                var command = new SocketReplacedInfo(oldSocketId);
                await EmitSocketCommand(command, oldSocketId);
            }

            await RegisterSocketCommandListener(socketHandler);
            socketHandler.Listen();
            _logger.LogDebug($"Initiated socket for player: {player.UserName} ({player.Id}), with Id: {socketContext.SocketId}");
            return socketContext;
        }

        public async Task UnRegisterSocket(SocketContext context)
        {
            await UnRegisterSocketCommandListener(context.SocketId);
            await _cache.CompareAndDeleteAsync(CurrentSocketKey(context.PlayerId), context.SocketId);
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
                _logger.LogWarning($"Attempted to emit command: {commandInfo} to player with no active socket: {playerId}");
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
            return async (IPubSubQueue queue) =>
            {
                var nextCommandInfo = await queue.GetNextAsync<SocketCommandInfo>();
                while (nextCommandInfo is not null)
                {
                    try
                    {
                        _logger.Log($"Received command on socket: {socket.Id}, playerId: {socket.PlayerId}, command: {nextCommandInfo}.");
                        await socket.ExecuteCommand(nextCommandInfo);
                        nextCommandInfo = await queue.GetNextAsync<SocketCommandInfo>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex);
                    }
                }
            };
        }

        private async Task<string?> CurrentSocketId(int playerId)
        {
            return await _cache.GetAsync(CurrentSocketKey(playerId));
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
