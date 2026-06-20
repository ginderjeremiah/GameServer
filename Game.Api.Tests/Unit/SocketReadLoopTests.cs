using Game.Abstractions.DataAccess;
using Game.Api.Services;
using Game.Api.Sockets;
using Game.Api.Sockets.Commands;
using Game.Application;
using Game.Core.Players;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Verifies the read-loop's terminal-fault handling (#953): a thrown receive that does not transition the
    /// WebSocket state must stop the loop and close the socket rather than tight-spinning on the same failure,
    /// while a client-initiated close frame still completes the closing handshake. Scripted <see cref="WebSocket"/>
    /// stand-ins keep these as plain unit tests.
    /// </summary>
    public sealed class SocketReadLoopTests : IDisposable
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        private readonly ServiceProvider _provider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILoggerFactory _loggerFactory;
        private readonly CapturingLoggerProvider _logs = new();

        public SocketReadLoopTests()
        {
            _provider = new ServiceCollection()
                .AddScoped<IUnitOfWork, NoOpUnitOfWork>()
                .BuildServiceProvider();
            _scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
            _loggerFactory = LoggerFactory.Create(b => b.AddProvider(_logs).SetMinimumLevel(LogLevel.Trace));
        }

        [Fact]
        public async Task ReadLoop_ReceiveThrowsWhileSocketStaysOpen_StopsAndClosesInsteadOfSpinning()
        {
            // A receive failure that does NOT transition WebSocketState (the socket stays Open) would loop the
            // read loop forever on the same throw — a tight CPU spin that floods the log. The fix treats a
            // thrown receive as terminal: the loop reads exactly once, then closes the socket so teardown can
            // complete.
            var socket = new ScriptedReadWebSocket();
            socket.QueueThrow(new WebSocketException("decode failure that leaves the socket open"));
            var (context, handler) = CreateHandler(socket);

            using var inactivityStop = new CancellationTokenSource();
            handler.Listen(hostStopping: inactivityStop.Token);

            await context.WaitSocketClosed().WaitAsync(WaitTimeout, TestContext.Current.CancellationToken);
            inactivityStop.Cancel();
            await handler.Completion.WaitAsync(WaitTimeout, TestContext.Current.CancellationToken);

            // The loop did not spin: it read once, then broke. And it closed the still-open socket gracefully.
            Assert.Equal(1, socket.ReceiveAttempts);
            Assert.True(socket.CloseAsyncCalled);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, socket.CloseStatusUsed);
            Assert.Contains(_logs.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("closing the socket"));
        }

        [Fact]
        public async Task ReadLoop_ClientCloseFrame_CompletesTheClosingHandshake()
        {
            // A client-initiated close still drives the read loop out of the CloseReceived state and completes
            // the handshake — the terminal-fault break must not regress the normal close path.
            var socket = new ScriptedReadWebSocket();
            socket.QueueClose();
            var (context, handler) = CreateHandler(socket);

            using var inactivityStop = new CancellationTokenSource();
            handler.Listen(hostStopping: inactivityStop.Token);

            await context.WaitSocketClosed().WaitAsync(WaitTimeout, TestContext.Current.CancellationToken);
            inactivityStop.Cancel();
            await handler.Completion.WaitAsync(WaitTimeout, TestContext.Current.CancellationToken);

            // The close frame was consumed (one read) and the loop exited via the CloseReceived state to
            // complete teardown, without retrying. SocketContext.Close only emits a close frame from the Open
            // state, so on CloseReceived it settles the close without re-sending one (no CloseAsync).
            Assert.Equal(1, socket.ReceiveAttempts);
            Assert.Equal(WebSocketState.CloseReceived, socket.State);
            // A clean client close is not an error.
            Assert.DoesNotContain(_logs.Entries, e => e.Level == LogLevel.Error);
        }

        private (SocketContext Context, SocketHandler Handler) CreateHandler(WebSocket socket)
        {
            var session = new SessionService(new NoOpSessionStore());
            session.CreateSession(userId: 1, playerId: 1);
            var context = new SocketContext(socket, playerId: 1, session, _loggerFactory.CreateLogger<SocketContext>());
            var handler = new SocketHandler(context, new StubCommandFactory(), _scopeFactory,
                _loggerFactory.CreateLogger<SocketHandler>(), () => Task.CompletedTask);
            return (context, handler);
        }

        public void Dispose()
        {
            _loggerFactory.Dispose();
            _provider.Dispose();
        }

        /// <summary>
        /// A <see cref="WebSocket"/> stand-in that scripts a single receive step (a thrown receive or a client
        /// close frame) and records the close frame, so the read loop's terminal-fault and clean-close paths can
        /// be driven deterministically. A throw leaves <see cref="State"/> Open — exactly the case that would
        /// spin the loop — and counts every receive so a test can assert the loop did not retry.
        /// </summary>
        private sealed class ScriptedReadWebSocket : WebSocket
        {
            private Exception? _throwOnReceive;
            private bool _closeFrameQueued;
            private WebSocketState _state = WebSocketState.Open;

            public int ReceiveAttempts { get; private set; }
            public bool CloseAsyncCalled { get; private set; }
            public WebSocketCloseStatus? CloseStatusUsed { get; private set; }

            public void QueueThrow(Exception toThrow) => _throwOnReceive = toThrow;
            public void QueueClose() => _closeFrameQueued = true;

            public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            {
                ReceiveAttempts++;
                if (_throwOnReceive is not null)
                {
                    // State deliberately stays Open: the receive throws without transitioning the socket, the
                    // exact case the read loop must treat as terminal rather than re-looping.
                    return Task.FromException<WebSocketReceiveResult>(_throwOnReceive);
                }

                if (_closeFrameQueued)
                {
                    _closeFrameQueued = false;
                    _state = WebSocketState.CloseReceived;
                    return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, endOfMessage: true));
                }

                // No further scripted input: block so a (buggy) spinning loop would be observable as repeated
                // receive attempts rather than a completed read.
                return new TaskCompletionSource<WebSocketReceiveResult>().Task;
            }

            public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            {
                CloseAsyncCalled = true;
                CloseStatusUsed = closeStatus;
                _state = WebSocketState.Closed;
                return Task.CompletedTask;
            }

            public override WebSocketState State => _state;
            public override WebSocketCloseStatus? CloseStatus => null;
            public override string? CloseStatusDescription => null;
            public override string? SubProtocol => null;
            public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => Task.CompletedTask;
            public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
            public override void Abort() { }
            public override void Dispose() { }
        }

        private sealed class StubCommandFactory : SocketCommandFactory
        {
            public override AbstractSocketCommand CreateCommand(SocketCommandInfo commandInfo, IServiceScope scope)
                => throw new NotSupportedException("These tests script receives only; no command is executed.");
        }

        private sealed class NoOpSessionStore : ISessionStore
        {
            public Task<PlayerState?> GetSession(int userId, CancellationToken cancellationToken = default) => Task.FromResult<PlayerState?>(null);
            public void Update(PlayerState sessionData, int playerId) { }
            public void Clear(int userId) { }
        }

        private sealed class NoOpUnitOfWork : IUnitOfWork
        {
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
