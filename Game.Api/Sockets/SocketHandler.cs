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

        // Serializes command execution across the two paths that reach ExecuteCommand — the client read
        // loop and the pub/sub processor (server-initiated commands) — so the documented "websocket
        // commands are handled sequentially for each player" guarantee holds for server pushes too,
        // preventing the read-modify-write lost-update race on the shared cached player state. Left
        // undisposed (like ReferenceCacheHolder's gate): a SemaphoreSlim only needs disposal once its
        // AvailableWaitHandle is materialized (never here), and skipping it avoids an ObjectDisposedException
        // race against an in-flight pub/sub command during socket teardown.
        private readonly SemaphoreSlim _commandLock = new(1, 1);

        /// <summary>
        /// Per-command execution budget. A command wedged on a slow or dead dependency would otherwise hold
        /// the command lock — and thus the player's whole command stream — indefinitely (today the socket
        /// only recovers when the 60s inactivity check closes it because no responses flow). On expiry the
        /// command is abandoned with a timeout response so the read/pub-sub loop keeps draining. Sits below
        /// the client's 30s per-request backstop (api-socket.ts) so the server's own timeout reaches the
        /// client first, and above StackExchange.Redis's 5s command timeout. Postgres (Npgsql) defaults to a
        /// 30s command timeout, so a genuinely slow DB call could be cut off a few seconds early — an
        /// accepted trade-off, since a >25s player command is already pathological and the client abandons it
        /// at 30s regardless.
        /// </summary>
        private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(25);

        private readonly TimeSpan _commandTimeout;

        private DateTime _lastResponse = DateTime.UtcNow;

        public string Id => _context.SocketId;
        public int PlayerId => _context.PlayerId;

        public SocketHandler(SocketContext context, SocketCommandFactory commandFactory, IServiceScopeFactory scopeFactory, ILogger<SocketHandler> logger, Func<Task> onActivity, TimeSpan? commandTimeout = null)
        {
            _context = context;
            _commandFactory = commandFactory;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _onActivity = onActivity;
            _commandTimeout = commandTimeout ?? DefaultCommandTimeout;
        }

        public void Listen()
        {
            Task.Run(ReadLoop);
            Task.Run(InactivityCheckerLoop);
        }

        public async Task ExecuteCommand(SocketCommandInfo commandInfo)
        {
            _logger.LogTrace("Executing command: {CommandInfo} on socket: {Id}", commandInfo, Id);
            await _commandLock.WaitAsync();

            // Cancels at the budget, both signalling cancellation-aware commands to unwind cooperatively and
            // bounding the WaitAsync below for commands that ignore the token.
            var cts = new CancellationTokenSource(_commandTimeout);
            var lockHandedOff = false;
            try
            {
                var commandTask = RunCommand(commandInfo, cts.Token);
                ApiSocketResponse response;
                try
                {
                    // Bound the wait without abandoning the underlying task: WaitAsync returns the command's
                    // response when it settles, or throws once the budget elapses (the command keeps running).
                    response = await commandTask.WaitAsync(cts.Token);
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    // Budget elapsed. Surface a timeout so the client — and the read/pub-sub loop — isn't
                    // left hanging, but do NOT release the command lock here: the abandoned task may still be
                    // mid read-modify-write of the shared cached Player, and releasing would let the next
                    // command race it (the lost-update class the per-socket command lock prevents — see
                    // docs/backend.md). Hand the lock to a continuation that releases it once the abandoned
                    // task finally settles. The abandoned command still commits its write (so it isn't lost),
                    // but its now-late response is dropped: RunCommand never sends — this method owns the single
                    // send — so the client never receives a second response for the same id.
                    _logger.LogWarning("Socket command timed out after {Timeout} and was abandoned: {CommandInfo} on socket: {Id}", _commandTimeout, commandInfo, Id);
                    await _context.SendData(new ApiSocketResponse
                    {
                        Id = commandInfo.Id,
                        Name = commandInfo.Name,
                        Error = "Command timed out."
                    });
                    ReleaseCommandLockWhenSettled(commandTask, cts, commandInfo);
                    lockHandedOff = true;
                    return;
                }

                await _context.SendData(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing a socket command: {CommandInfo}", commandInfo);
                await _context.SendData(new ApiSocketResponse
                {
                    Id = commandInfo.Id,
                    Name = commandInfo.Name,
                    Error = "Internal Server Error"
                });
            }
            finally
            {
                if (!lockHandedOff)
                {
                    cts.Dispose();
                    _commandLock.Release();
                }
            }
        }

        // Executes the command and commits its unit of work, returning the response for the caller to send.
        // It deliberately does not send: ExecuteCommand owns the single send so that a command abandoned on
        // timeout (whose task runs on here in the background) commits its write but never emits a second,
        // late response for an id the client already saw time out.
        private async Task<ApiSocketResponse> RunCommand(SocketCommandInfo commandInfo, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var command = _commandFactory.CreateCommand(commandInfo, scope);
            var response = await command.ExecuteAsync(_context, cancellationToken);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            return response;
        }

        /// <summary>
        /// Releases the command lock once an abandoned (timed-out) command finally settles, preserving the
        /// per-socket serialization guarantee: the next command waits on the lock rather than racing the
        /// still-running one. The continuation runs immediately when the command already completed (e.g. a
        /// cooperative cancellation), so this is used for both the cooperative and the wedged paths.
        /// </summary>
        private void ReleaseCommandLockWhenSettled(Task commandTask, CancellationTokenSource cts, SocketCommandInfo commandInfo)
        {
            _ = commandTask.ContinueWith(task =>
            {
                // The client already received a timeout response; a cooperative cancellation surfaces here as
                // the expected OperationCanceledException, while any other fault is genuine and worth logging.
                if (task.Exception is { } fault && fault.InnerExceptions.Any(e => e is not OperationCanceledException))
                {
                    _logger.LogError(fault, "Abandoned socket command faulted after its timeout response was sent: {CommandInfo} on socket: {Id}.", commandInfo, Id);
                }

                cts.Dispose();
                _commandLock.Release();
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
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
                    if (_commandFactory.IsServerInitiatedOnly(commandInfo.Name))
                    {
                        // Server-initiated commands (e.g. ChallengeCompleted, SocketReplaced) are only valid
                        // when dispatched via the backplane; a client sending one is rejected, not executed.
                        _logger.LogWarning("Client attempted to invoke server-initiated command: {CommandInfo} on socket: {Id}", commandInfo, Id);
                        await _context.SendData(new ApiSocketResponse
                        {
                            Id = commandInfo.Id,
                            Name = commandInfo.Name,
                            Error = "Command cannot be invoked by the client."
                        });
                        return;
                    }

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
