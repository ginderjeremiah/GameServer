using Game.Api.Models.Common;
using Game.Api.Services;
using Game.Api.Sockets.Commands;
using Game.Application;
using Game.Core;
using System.Text.Json;
using static System.Net.WebSockets.WebSocketState;

namespace Game.Api.Sockets
{
    public class SocketHandler
    {
        private readonly SocketContext _context;
        private readonly SocketCommandFactory _commandFactory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SocketHandler> _logger;

        private DateTime _lastSend = DateTime.UtcNow;
        private DateTime _lastResponse = DateTime.UtcNow;

        private DateTime LastAction => _lastResponse > _lastSend ? _lastResponse : _lastSend;

        public string Id => _context.SocketId;
        public int PlayerId => _context.PlayerId;

        public SocketHandler(SocketContext context, SocketCommandFactory commandFactory, IServiceScopeFactory scopeFactory, ILogger<SocketHandler> logger)
        {
            _context = context;
            _commandFactory = commandFactory;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public void Listen()
        {
            Task.Run(ReadLoop);
            Task.Run(PingLoop);
        }

        public async Task ExecuteCommand(SocketCommandInfo commandInfo)
        {
            _logger.LogTrace("Executing command: {CommandInfo} on socket: {Id}", commandInfo, Id);
            using var scope = _scopeFactory.CreateScope();
            var command = await _commandFactory.CreateCommand(commandInfo, scope);
            try
            {
                var response = await command.ExecuteAsync(_context);
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
                _context.Session.ClearPlayerDomainEvents();
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
            while (DateTime.UtcNow - _lastResponse < TimeSpan.FromSeconds(60) && _context.State is Open)
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

            if (_context.State is Open)
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
                    _logger.LogError(ex, "An error occurred while reading a socket message.");
                }
            }
            while (_context.State is Open);

            if (_context.State is CloseReceived)
            {
                await _context.Close(ESocketCloseReason.Finished);
            }
        }

        private async Task HandleMessage(string message)
        {
            if (message == "pong")
            {
                return;
            }

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
