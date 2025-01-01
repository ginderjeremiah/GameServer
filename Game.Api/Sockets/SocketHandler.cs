using Game.Api.Models.Common;
using Game.Api.Services;
using Game.Api.Sockets.Commands;
using Game.Core;
using System.Net.WebSockets;
using System.Text.Json;
using static System.Net.WebSockets.WebSocketState;

namespace Game.Api.Sockets
{
    public class SocketHandler
    {
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

        public async Task ExecuteCommand(SocketCommandInfo commandInfo)
        {
            _logger.LogTrace("Executing command: {CommandInfo} on socket: {Id}", commandInfo, Id);
            var command = await _commandFactory.CreateCommand(commandInfo);
            try
            {
                var response = await command.ExecuteAsync(_context);
                await _context.SendData(response);
            }
            catch
            {
                await _context.SendData(new ApiSocketResponse
                {
                    Id = commandInfo.Id,
                    Error = "Internal Server Error"
                });
            }

            _lastSend = DateTime.UtcNow;
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
                        await _context.SendData("ping");
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
                    var message = await _context.ReadMessage();
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        _logger.LogDebug("Received socket data from playerId ({PlayerId}) on socket ({Id}): {Message}", PlayerId, Id, message);
                        _lastResponse = DateTime.UtcNow;
                        await HandleMessage(message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occured while reading a socket message.");
                }
            }
            while (Socket.State is Open);

            if (Socket.State is CloseReceived)
            {
                await _context.Close(ESocketCloseReason.Finished);
            }
        }

        private async Task HandleMessage(string message)
        {
            if (message != "pong")
            {
                try
                {
                    var commandInfo = message.Deserialize<SocketCommandInfo>();
                    if (commandInfo is not null)
                    {
                        await ExecuteCommand(commandInfo);
                    }
                }
                catch (JsonException)
                {
                    _logger.LogWarning("Failed to deserialize socket command: {Message}", message);
                }
            }
        }
    }
}

