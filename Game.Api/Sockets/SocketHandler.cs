using Game.Api.Models.Common;
using Game.Api.Services;
using Game.Api.Sockets.Commands;
using Game.Application;
using Game.Core;
using System.Diagnostics;
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
        private readonly Action _onActivity;

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

        /// <summary>How long a socket may go without inbound traffic before the watchdog closes it.</summary>
        private static readonly TimeSpan InactivityTimeout = TimeSpan.FromSeconds(60);

        /// <summary>How often the inactivity watchdog re-checks the last-activity timestamp.</summary>
        private static readonly TimeSpan InactivityPollInterval = TimeSpan.FromSeconds(10);

        // Written by the read loop and read by the inactivity loop on separate threads, so it is accessed via
        // Interlocked to give the cross-thread happens-before edge a plain DateTime field would lack — a
        // DateTime is a 16-byte struct that isn't even guaranteed to be read atomically (a torn read is
        // possible). Stored as ticks because Interlocked operates on a long.
        private long _lastResponseTicks = DateTime.UtcNow.Ticks;

        // The read and inactivity loops, tracked (rather than pure fire-and-forget) so a host shutdown can
        // await their completion within a bounded drain window — see SocketConnectionRegistry.
        private Task _loops = Task.CompletedTask;

        public string Id => _context.SocketId;
        public int PlayerId => _context.PlayerId;

        /// <summary>Completes once both the read and inactivity loops have wound down.</summary>
        public Task Completion => _loops;

        public SocketHandler(SocketContext context, SocketCommandFactory commandFactory, IServiceScopeFactory scopeFactory, ILogger<SocketHandler> logger, Action onActivity, TimeSpan? commandTimeout = null)
        {
            _context = context;
            _commandFactory = commandFactory;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _onActivity = onActivity;
            _commandTimeout = commandTimeout ?? DefaultCommandTimeout;
        }

        /// <summary>
        /// Starts the per-socket read and inactivity loops, tying both to the host lifetime so a draining
        /// instance can tear them down cleanly (#526). The two tokens carry different shutdown phases:
        /// <paramref name="hostStopping"/> fires the moment graceful shutdown begins and stops the
        /// inactivity watchdog (its job is moot once the drain owns teardown), while
        /// <paramref name="drainDeadline"/> fires only if the bounded drain window elapses and aborts a
        /// blocking <see cref="SocketContext.ReadMessage"/> that is still waiting on a client that never
        /// completed the closing handshake. Outside of shutdown neither token is cancelled, so behaviour is
        /// unchanged.
        /// </summary>
        public void Listen(CancellationToken hostStopping = default, CancellationToken drainDeadline = default)
        {
            _loops = Task.WhenAll(
                Task.Run(() => ReadLoop(drainDeadline)),
                Task.Run(() => InactivityCheckerLoop(hostStopping)));
        }

        /// <summary>
        /// Gracefully closes the socket for a host shutdown: sends a <see cref="ESocketCloseReason.ServerShuttingDown"/>
        /// close frame so the client reconnects to a healthy instance rather than hanging on a half-open
        /// socket, then waits for the loops to wind down. <paramref name="drainDeadline"/> bounds the close
        /// frame's own send; the loops are awaited unconditionally since they never fault on cancellation.
        /// </summary>
        public async Task ShutdownAsync(CancellationToken drainDeadline)
        {
            try
            {
                await _context.Close(ESocketCloseReason.ServerShuttingDown, drainDeadline);
            }
            catch (OperationCanceledException)
            {
                // The drain window elapsed mid-close; the read loop is aborted via the same token below.
            }

            await Completion;
        }

        /// <summary>
        /// Executes a command from the client read loop. The client awaits this command's response by id, so
        /// a genuine fault is surfaced to it as an <c>"Internal Server Error"</c> response it can react to.
        /// </summary>
        public async Task ExecuteCommand(SocketCommandInfo commandInfo)
        {
            _logger.LogTrace("Executing command: {CommandInfo} on socket: {Id}", commandInfo, Id);
            var (outcome, fault) = await RunCommandUnderLock(commandInfo);
            if (outcome is SocketCommandOutcome.Faulted)
            {
                _logger.LogError(fault, "An error occurred while executing a socket command: {CommandInfo}", commandInfo);
                await SendErrorAsync(commandInfo, "Internal Server Error");
            }
        }

        /// <summary>
        /// Executes a server-initiated (pub/sub) command and reports the outcome so the
        /// <see cref="Services.SocketManagerService"/> processor can escalate a fault (dead-letter + a typed
        /// client re-sync notice) rather than silently dropping it. Unlike <see cref="ExecuteCommand"/> it
        /// does <b>not</b> send an <c>"Internal Server Error"</c> response on a fault: a server push carries no
        /// awaiting client request, so the processor owns the surfacing instead.
        /// </summary>
        internal async Task<SocketCommandOutcome> ExecuteServerCommand(SocketCommandInfo commandInfo)
        {
            _logger.LogTrace("Executing server-initiated command: {CommandInfo} on socket: {Id}", commandInfo, Id);
            var (outcome, fault) = await RunCommandUnderLock(commandInfo);
            if (outcome is SocketCommandOutcome.Faulted)
            {
                // Logged here (with the exception) so the failure is captured once; the processor logs only
                // the escalation it then performs.
                _logger.LogError(fault, "A server-initiated socket command failed: {CommandInfo} on socket: {Id}", commandInfo, Id);
            }

            return outcome;
        }

        /// <summary>
        /// Runs a command under the per-socket command lock and per-command timeout, classifying the result
        /// into a <see cref="SocketCommandOutcome"/> so the two callers can apply their own fault policy. It
        /// owns the single send for the success and timeout paths (so an abandoned command's late response is
        /// suppressed) but sends nothing on a fault — the caller decides how to surface that. A cancellation
        /// that is not the per-command timeout is treated as a lifetime/teardown unwind, not a command defect
        /// (#671).
        /// </summary>
        private async Task<(SocketCommandOutcome Outcome, Exception? Fault)> RunCommandUnderLock(SocketCommandInfo commandInfo)
        {
            var startTimestamp = Stopwatch.GetTimestamp();
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
                    await SendErrorAsync(commandInfo, "Command timed out.");
                    ReleaseCommandLockWhenSettled(commandTask, cts, commandInfo);
                    lockHandedOff = true;
                    return (SocketCommandOutcome.TimedOut, null);
                }

                await _context.SendData(response);
                return (SocketCommandOutcome.Succeeded, null);
            }
            catch (OperationCanceledException ex)
            {
                // A cancellation that is NOT the per-command timeout (handled above with the
                // cts.IsCancellationRequested guard) is a lifetime/teardown cancellation, not a command
                // defect: log it as a teardown rather than surfacing a misleading "Internal Server Error",
                // and send no response since the socket is unwinding (#671).
                _logger.LogDebug(ex, "Socket command cancelled during teardown: {CommandInfo} on socket: {Id}", commandInfo, Id);
                return (SocketCommandOutcome.TornDown, ex);
            }
            catch (Exception ex)
            {
                // A genuine command fault. The caller owns surfacing it (a client error response, or the
                // server-push escalation), so no response is sent here.
                return (SocketCommandOutcome.Faulted, ex);
            }
            finally
            {
                var elapsedTime = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                _logger.LogTrace("Socket command {CommandInfoName} executed in {ElapsedTime}ms.", commandInfo.Name, elapsedTime);
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

        private async Task InactivityCheckerLoop(CancellationToken hostStopping)
        {
            try
            {
                while (DateTime.UtcNow.Ticks - Interlocked.Read(ref _lastResponseTicks) < InactivityTimeout.Ticks && _context.State is Open)
                {
                    await Task.Delay(InactivityPollInterval, hostStopping);
                }
            }
            catch (OperationCanceledException)
            {
                // The host is shutting down; the drain coordinator now owns closing this socket, so the
                // watchdog simply stops rather than racing it with an Inactivity close.
                return;
            }

            if (_context.State is Open)
            {
                await _context.Close(ESocketCloseReason.Inactivity);
            }
        }

        private async Task ReadLoop(CancellationToken drainDeadline)
        {
            do
            {
                try
                {
                    var message = await _context.ReadMessage(drainDeadline);
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        _logger.LogDebug("Received socket data from playerId ({PlayerId}) on socket ({Id}): {Message}", PlayerId, Id, message);
                        Interlocked.Exchange(ref _lastResponseTicks, DateTime.UtcNow.Ticks);
                        // Any inbound message (heartbeat ping or command) marks the connection live — keep its
                        // presence key from expiring on the same signal the inactivity check uses above. Presence
                        // refresh is best-effort (the sliding TTL lapses harmlessly if a refresh is missed), so
                        // isolate a fault here: it must never reach the terminal read-fault catch below and tear
                        // down an otherwise-healthy connection.
                        try
                        {
                            _onActivity();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to refresh socket presence for player ({PlayerId}) on socket ({Id}); the read loop continues.", PlayerId, Id);
                        }

                        await HandleMessage(message);
                    }
                }
                catch (OperationCanceledException) when (drainDeadline.IsCancellationRequested)
                {
                    // The graceful drain window elapsed and the blocking receive was aborted; stop reading
                    // so the loop (and thus the drain) can complete.
                    break;
                }
                catch (Exception ex)
                {
                    // A thrown receive is terminal: the socket can't be read from again, so stop reading
                    // rather than looping. Most receive failures abort the socket (state leaves Open and the
                    // do…while would exit anyway), but a failure that does NOT transition WebSocketState (e.g.
                    // an OOM/decoding error while assembling the message) would otherwise leave the loop a
                    // tight CPU spin that also floods the log — and the inactivity watchdog can't help because
                    // _lastResponseTicks only advances on a successful read. Only log while still open, since a
                    // receive throwing on an already-closing socket is expected.
                    if (_context.State is Open)
                    {
                        _logger.LogError(ex, "An error occurred while reading a socket message; closing the socket.");
                    }

                    break;
                }
            }
            while (_context.State is Open);

            // Complete the closing handshake for a client-initiated close, and close a socket the read loop is
            // abandoning while still open (the terminal-fault break above) so teardown completes instead of
            // leaving a half-open socket. Close is a no-op on an already-closed/aborted socket.
            if (_context.State is Open or CloseReceived)
            {
                await _context.Close(ESocketCloseReason.Finished);
            }
        }

        internal async Task HandleMessage(string message)
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
                    if (string.IsNullOrEmpty(commandInfo.Name))
                    {
                        // A structurally-valid frame can omit "name" (System.Text.Json binds it null), so reject
                        // it with a structured error instead of letting the null reach the command lookup — which
                        // would throw an unobserved exception and leave the client hanging on its request id.
                        _logger.LogWarning("Received socket command frame with no name: {Message} on socket: {Id}", message, Id);
                        await SendErrorAsync(commandInfo, "Malformed command.");
                        return;
                    }

                    if (_commandFactory.IsServerInitiatedOnly(commandInfo.Name))
                    {
                        // Server-initiated commands (e.g. ChallengeCompleted, SocketReplaced) are only valid
                        // when dispatched via the backplane; a client sending one is rejected, not executed.
                        _logger.LogWarning("Client attempted to invoke server-initiated command: {CommandInfo} on socket: {Id}", commandInfo, Id);
                        await SendErrorAsync(commandInfo, "Command cannot be invoked by the client.");
                        return;
                    }

                    if (!_commandFactory.IsKnownCommand(commandInfo.Name))
                    {
                        // An unknown command name (a stale/garbage name, realistic across deploys) is a
                        // bad request, not an internal fault: reject it with a structured error like the
                        // sibling rejections above instead of letting it reach the command lookup, whose
                        // throw would be logged at error and surfaced to the client as "Internal Server Error".
                        _logger.LogWarning("Received unknown socket command: {CommandInfo} on socket: {Id}", commandInfo, Id);
                        await SendErrorAsync(commandInfo, "Unknown command.");
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

        /// <summary>
        /// Sends an error response frame for a command, echoing its id and name so the client can correlate the
        /// failure with the request it is awaiting. The single place the error-frame shape is built, so every
        /// failure path (fault, timeout, malformed/rejected frame) emits a consistent envelope.
        /// </summary>
        private Task SendErrorAsync(SocketCommandInfo commandInfo, string error)
        {
            return _context.SendData(new ApiSocketResponse
            {
                Id = commandInfo.Id,
                Name = commandInfo.Name,
                Error = error
            });
        }
    }
}
