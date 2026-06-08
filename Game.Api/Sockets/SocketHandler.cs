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
        private readonly Func<Task> _onActivity;

        private DateTime _lastResponse = DateTime.UtcNow;

        public string Id => _context.SocketId;
        public int PlayerId => _context.PlayerId;

        public SocketHandler(SocketContext context, SocketCommandFactory commandFactory, IServiceScopeFactory scopeFactory, ILogger<SocketHandler> logger, Func<Task> onActivity)
        {
            _context = context;
            _commandFactory = commandFactory;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _onActivity = onActivity;
        }

        public void Listen()
        {
            Task.Run(ReadLoop);
            Task.Run(InactivityCheckerLoop);
        }

        public async Task ExecuteCommand(SocketCommandInfo commandInfo)
        {
            _logger.LogTrace("Executing command: {CommandInfo} on socket: {Id}", commandInfo, Id);
            using var scope = _scopeFactory.CreateScope();
            var command = _commandFactory.CreateCommand(commandInfo, scope);
            try
            {
                var response = await command.ExecuteAsync(_context);
                await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
                await _context.SendData(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing a socket command: {CommandInfo}", commandInfo);
                await _context.SendData(new ApiSocketResponse
                {
                    Id = commandInfo.Id,
                    Error = "Internal Server Error"
                });
            }
        }

        private async Task InactivityCheckerLoop()
        {
            while (DateTime.UtcNow - _lastResponse < TimeSpan.FromSeconds(60) && _context.State is Open)
            {
                await Task.Delay(10000);
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
                        // Any inbound message (heartbeat ping or command) marks the connection live — keep its
                        // presence key from expiring on the same signal the inactivity check uses above.
                        await _onActivity();
                        await HandleMessage(message);
                    }
                }
                catch (Exception ex)
                {
                    if (_context.State is Open) // Only log if the socket is still open, otherwise it's expected that exceptions may occur
                    {
                        _logger.LogError(ex, "An error occurred while reading a socket message.");
                    }
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
            if (message is "pong")
            {
                return;
            }

            if (message is "ping")
            {
                await _context.SendData("pong");
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
