﻿using GameCore;
using GameCore.Infrastructure;
using GameInfrastructure;
using GameServer.Sockets;
using GameServer.Sockets.Commands;

namespace GameServer.Services
{
    public class SocketManagerService
    {
        private readonly IPubSubService _pubSub;
        private readonly ICacheService _cache;
        private readonly IApiLogger _logger;

        public SocketManagerService(IDataServicesFactory dataServices)
        {
            _pubSub = dataServices.PubSub;
            _cache = dataServices.Cache;
            _logger = dataServices.Logger;
        }

        public async Task RegisterSocket(SocketHandler socket)
        {
            var oldSocketId = await _cache.GetSetAsync(CurrentSocketKey(socket.PlayerId), socket.Id);
            if (oldSocketId is not null)
            {
                var command = new SocketReplacedInfo(oldSocketId);
                await EmitSocketCommand(command, oldSocketId);
                await UnRegisterSocketCommandListener(oldSocketId);
            }

            await RegisterSocketCommandListener(socket);
            socket.Listen();
        }

        public async Task UnRegisterSocket(SocketHandler socket)
        {
            await UnRegisterSocketCommandListener(socket.Id);
            await _cache.CompareAndDeleteAsync(CurrentSocketKey(socket.PlayerId), socket.Id);
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

        private async Task RegisterSocketCommandListener(SocketHandler socket)
        {
            var processor = GetSocketCommandProcessor(socket);
            await _pubSub.Subscribe(SocketChannel(socket.Id), SocketQueueName(socket.Id), async args => await processor(args.queue), socket.Id);
            await _pubSub.Publish(SocketChannel(socket.Id), "");
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
                        _logger.Log($"Recieved command on socket: {socket.Id}, playerId: {socket.PlayerId}, command: {nextCommandInfo}.");
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
