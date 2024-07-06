using GameCore;
using GameServer.Models.Common;
using GameServer.Services;
using GameServer.Sockets.Commands;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using static System.Net.WebSockets.WebSocketState;

namespace GameServer.Sockets
{
    public class SocketHandler
    {
        private readonly byte[] _buffer = new byte[1024 * 4];
        private readonly WebSocket _socket;
        private readonly SocketCommandFactory _commandFactory;
        private readonly IApiLogger _logger;
        private DateTime _lastSend = DateTime.UtcNow;
        private DateTime _lastResponse = DateTime.UtcNow;

        private DateTime LastAction => _lastResponse > _lastSend ? _lastResponse : _lastSend;

        public string Id { get; }
        public TaskCompletionSource<ESocketCloseReason> SocketFinished { get; } = new();
        public int PlayerId { get; }

        public SocketHandler(WebSocket socket, SocketCommandFactory commandFactory, IApiLogger logger, int playerId)
        {
            Id = Guid.NewGuid().ToString();
            PlayerId = playerId;
            _socket = socket;
            _commandFactory = commandFactory;
            _logger = logger;
        }

        public void Listen()
        {
            Task.Run(ReadLoop);
            Task.Run(PingLoop);
        }

        public async Task<bool> SendData(string data)
        {
            _logger.LogDebug($"Sending data to playerId ({PlayerId}) from socket ({Id}): {data}");
            var dataBytes = Encoding.UTF8.GetBytes(data);
            try
            {
                await _socket.SendAsync(new ArraySegment<byte>(dataBytes, 0, dataBytes.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                _lastSend = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                return false;
            }

            return true;
        }

        public async Task<bool> SendData<T>(T data) where T : ApiSocketResponse
        {
            return await SendData(data.Serialize());
        }

        public async Task ExecuteCommand(SocketCommandInfo commandInfo)
        {
            _logger.Log($"Executing command: {commandInfo} on socket: {Id}");
            var command = _commandFactory.CreateCommand(commandInfo);
            var response = await command.Execute();
            if (response.CloseReason is not null)
            {
                Close(response.CloseReason.Value);
            }
            else
            {
                response.Id = commandInfo.Id;
                await SendData(response);
            }
        }

        public void Close(ESocketCloseReason closeReason = ESocketCloseReason.Finished)
        {
            SocketFinished.TrySetResult(closeReason);
        }

        private void ClearBuffer()
        {
            for (var i = 0; i < _buffer.Length; i++)
            {
                if (_buffer[i] != 0)
                {
                    _buffer[i] = 0;
                }
                else
                {
                    return;
                }
            }
        }

        private int LastNonZeroBufferByte()
        {
            for (var i = 0; i < _buffer.Length; i++)
            {
                if (_buffer[i] == 0)
                {
                    return i;
                }
            }

            return _buffer.Length - 1;
        }

        private async Task PingLoop()
        {
            while (DateTime.UtcNow - _lastResponse < TimeSpan.FromSeconds(60) && _socket.State is Open)
            {
                await Task.Delay(5000);
                if (DateTime.UtcNow - LastAction > TimeSpan.FromSeconds(20))
                {
                    try
                    {
                        await SendData("ping");
                        _lastSend = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex);
                    }
                }
            }

            if (_socket.State is Open)
            {
                Close(ESocketCloseReason.Inactivity);
            }
        }

        private async Task ReadLoop()
        {
            do
            {
                try
                {
                    ClearBuffer();
                    await _socket.ReceiveAsync(new ArraySegment<byte>(_buffer), CancellationToken.None);
                    var data = Encoding.UTF8.GetString(_buffer, 0, LastNonZeroBufferByte());
                    if (!string.IsNullOrWhiteSpace(data))
                    {
                        _logger.LogDebug($"Received data from playerId ({PlayerId}) on socket ({Id}): {data}");
                        _lastResponse = DateTime.UtcNow;
                        if (data != "pong")
                        {
                            try
                            {
                                var commandInfo = data.Deserialize<SocketCommandInfo>();
                                if (commandInfo is not null)
                                {
                                    await ExecuteCommand(commandInfo);
                                }
                            }
                            catch (JsonException)
                            {
                                _logger.LogWarning($"Failed to deserialize socket command: {data}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex);
                }
            }
            while (_socket.State is Open);

            if (_socket.State is CloseReceived)
            {
                Close(ESocketCloseReason.Finished);
            }
        }
    }
}

