using Game.Api.Models.Common;
using Game.Api.Services;
using Game.Api.Sockets.Commands;
using Game.Core;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using static System.Net.WebSockets.WebSocketState;

namespace Game.Api.Sockets
{
    public class SocketHandler
    {
        private const short MAX_MESSAGE_SIZE = 1024 * 4;
        private readonly byte[] _buffer = new byte[MAX_MESSAGE_SIZE];

        private readonly SocketCommandFactory _commandFactory;
        private readonly ILogger<SocketHandler> _logger;
        private readonly SocketContext _context;

        private DateTime _lastSend = DateTime.UtcNow;
        private DateTime _lastResponse = DateTime.UtcNow;

        private DateTime LastAction => _lastResponse > _lastSend ? _lastResponse : _lastSend;

        private WebSocket Socket => _context.Socket;
        public string Id => _context.SocketId;
        public int PlayerId => _context.PlayerId;

        public SocketHandler(SocketContext context, SocketCommandFactory commandFactory, ILogger<SocketHandler> logger)
        {
            _commandFactory = commandFactory;
            _logger = logger;
            _context = context;
        }

        public void Listen()
        {
            Task.Run(ReadLoop);
            Task.Run(PingLoop);
        }

        public async Task<bool> SendData(string data)
        {
            if (Socket.State is not Open)
                return false;

            _logger.LogDebug("Sending data to playerId ({PlayerId}) from socket ({Id}): {Data}", PlayerId, Id, data);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            try
            {
                for (int i = 0; i < dataBytes.Length; i += MAX_MESSAGE_SIZE)
                {
                    if (dataBytes.Length - i <= MAX_MESSAGE_SIZE)
                    {
                        await Socket.SendAsync(new ArraySegment<byte>(dataBytes, i, dataBytes.Length - i), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    else
                    {
                        await Socket.SendAsync(new ArraySegment<byte>(dataBytes, i, MAX_MESSAGE_SIZE), WebSocketMessageType.Text, false, CancellationToken.None);
                    }

                    _lastSend = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send socket data: {Data}", data);
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
            _logger.LogTrace("Executing command: {CommandInfo} on socket: {Id}", commandInfo, Id);
            var command = await _commandFactory.CreateCommand(commandInfo);
            try
            {
                var response = await command.ExecuteAsync(_context);
                await SendData(response);
            }
            catch
            {
                await SendData(new ApiSocketResponse
                {
                    Id = commandInfo.Id,
                    Error = "Internal Server Error"
                });
            }
        }

        private void ClearBuffer(int end = -1)
        {
            var last = end > 0 ? end : _buffer.Length - 1;

            for (var i = 0; i <= last; i++)
            {
                _buffer[i] = 0;
            }
        }

        private async Task PingLoop()
        {
            while (DateTime.UtcNow - _lastResponse < TimeSpan.FromSeconds(60) && Socket.State is Open)
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
                        _logger.LogWarning(ex, "An error occurred while pinging a client socket");
                    }
                }
            }

            if (Socket.State is Open)
            {
                await _context.Close(ESocketCloseReason.Inactivity);
            }
        }

        private async Task ReadLoop()
        {
            do
            {
                try
                {
                    string? msg = null;
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await Socket.ReceiveAsync(new ArraySegment<byte>(_buffer), CancellationToken.None);
                        msg = $"{msg}{ReadBuffer()}";
                    }
                    while (!result.EndOfMessage);

                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        _logger.LogDebug("Received socket data from playerId ({PlayerId}) on socket ({Id}): {Msg}", PlayerId, Id, msg);
                        _lastResponse = DateTime.UtcNow;
                        if (msg != "pong")
                        {
                            try
                            {
                                var commandInfo = msg.Deserialize<SocketCommandInfo>();
                                if (commandInfo is not null)
                                {
                                    await ExecuteCommand(commandInfo);
                                }
                            }
                            catch (JsonException)
                            {
                                _logger.LogWarning("Failed to deserialize socket command: {Msg}", msg);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occured while reading a socket message");
                }
            }
            while (Socket.State is Open);

            if (Socket.State is CloseReceived)
            {
                await _context.Close(ESocketCloseReason.Finished);
            }
        }

        private string ReadBuffer()
        {
            var lastNonZeroByte = -1;
            for (var i = _buffer.Length - 1; i >= 0 && lastNonZeroByte == -1; i--)
            {
                if (_buffer[i] != 0)
                {
                    lastNonZeroByte = i;
                }
            }

            var str = Encoding.UTF8.GetString(_buffer, 0, lastNonZeroByte + 1);
            ClearBuffer(lastNonZeroByte);
            return str;
        }
    }
}

