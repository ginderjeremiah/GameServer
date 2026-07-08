using Game.Abstractions.DataAccess;
using Game.Api.Services;
using Game.Api.Sockets;
using Game.Api.Sockets.Commands;
using Game.Application;
using Game.Core.Players;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Pins the inactivity watchdog end-to-end (#1726): an idle socket (no inbound traffic at all) is actually
    /// closed with <see cref="ESocketCloseReason.Inactivity"/> once <c>InactivityTimeout</c> elapses.
    /// <see cref="SocketHandler"/>'s inactivity timeout/poll interval are injectable (mirroring
    /// <c>commandTimeout</c>) specifically so this can run as a fast, deterministic unit test rather than
    /// waiting out the real 60s default.
    /// </summary>
    public sealed class SocketInactivityTests : IDisposable
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        private readonly ServiceProvider _provider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILoggerFactory _loggerFactory;

        public SocketInactivityTests()
        {
            _provider = new ServiceCollection()
                .AddScoped<IUnitOfWork, NoOpUnitOfWork>()
                .BuildServiceProvider();
            _scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();
            _loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Trace));
        }

        [Fact]
        public async Task IdleSocket_InactivityTimeoutElapses_ClosesWithInactivityReason()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new IdleWebSocket();
            var session = new SessionService(new NoOpSessionStore());
            var context = new SocketContext(socket, playerId: 1, session, isAdmin: false, _loggerFactory.CreateLogger<SocketContext>());
            var handler = new SocketHandler(context, new StubCommandFactory(), _scopeFactory, _loggerFactory.CreateLogger<SocketHandler>(),
                () => { }, inactivityTimeout: TimeSpan.FromMilliseconds(80), inactivityPollInterval: TimeSpan.FromMilliseconds(20));

            using var drainDeadline = new CancellationTokenSource();
            handler.Listen(drainDeadline: drainDeadline.Token);

            // No message is ever received, so only the watchdog can be what closes this socket.
            await context.WaitSocketClosed().WaitAsync(WaitTimeout, cancellationToken);

            Assert.True(socket.CloseAsyncCalled);
            Assert.Equal(WebSocketCloseStatus.PolicyViolation, socket.CloseStatusUsed);
            Assert.NotNull(socket.CloseDescriptionUsed);
            Assert.Contains("inactivity", socket.CloseDescriptionUsed, StringComparison.OrdinalIgnoreCase);

            // Tear down: abort the still-blocked read loop's receive so the loops complete cleanly.
            drainDeadline.Cancel();
            await handler.Completion.WaitAsync(WaitTimeout, cancellationToken);
        }

        [Fact]
        public async Task ActiveSocket_InactivityTimeoutElapses_IsNotClosed()
        {
            // A socket that keeps receiving well within the timeout must never be closed by the watchdog —
            // guards the fix above against a regression that closes on a fixed schedule instead of tracking
            // last-activity.
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new IdleWebSocket();
            var session = new SessionService(new NoOpSessionStore());
            var context = new SocketContext(socket, playerId: 1, session, isAdmin: false, _loggerFactory.CreateLogger<SocketContext>());
            // A generous margin (10x the delivery cadence below) so a loaded CI runner overshooting a single
            // Task.Delay can't flake this into a false watchdog firing.
            var handler = new SocketHandler(context, new StubCommandFactory(), _scopeFactory, _loggerFactory.CreateLogger<SocketHandler>(),
                () => { }, inactivityTimeout: TimeSpan.FromMilliseconds(500), inactivityPollInterval: TimeSpan.FromMilliseconds(20));

            using var drainDeadline = new CancellationTokenSource();
            handler.Listen(drainDeadline: drainDeadline.Token);

            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(50, cancellationToken);
                socket.DeliverMessage("ping");
            }

            Assert.False(socket.CloseAsyncCalled);

            drainDeadline.Cancel();
            await handler.Completion.WaitAsync(WaitTimeout, cancellationToken);
        }

        public void Dispose()
        {
            _loggerFactory.Dispose();
            _provider.Dispose();
        }

        /// <summary>
        /// A <see cref="WebSocket"/> stand-in whose <see cref="ReceiveAsync"/> blocks until either a message is
        /// delivered (<see cref="DeliverMessage"/>) or the caller's token cancels — modelling a socket with no
        /// inbound traffic so only the inactivity watchdog can close it. Delivered messages queue rather than
        /// overwrite a pending receive, so a message sent before the loop has looped back to its next receive
        /// call is never silently dropped.
        /// </summary>
        private sealed class IdleWebSocket : WebSocket
        {
            private readonly Channel<string> _incoming = Channel.CreateUnbounded<string>();
            private WebSocketState _state = WebSocketState.Open;

            public bool CloseAsyncCalled { get; private set; }
            public WebSocketCloseStatus? CloseStatusUsed { get; private set; }
            public string? CloseDescriptionUsed { get; private set; }

            public void DeliverMessage(string message) => _incoming.Writer.TryWrite(message);

            public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            {
                var message = await _incoming.Reader.ReadAsync(cancellationToken);
                var bytes = Encoding.UTF8.GetBytes(message);
                bytes.CopyTo(buffer.Array.AsSpan(buffer.Offset));
                return new WebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, endOfMessage: true);
            }

            public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => Task.CompletedTask;

            public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            {
                CloseAsyncCalled = true;
                CloseStatusUsed = closeStatus;
                CloseDescriptionUsed = statusDescription;
                _state = WebSocketState.Closed;
                return Task.CompletedTask;
            }

            public override WebSocketState State => _state;
            public override WebSocketCloseStatus? CloseStatus => null;
            public override string? CloseStatusDescription => null;
            public override string? SubProtocol => null;
            public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
            public override void Abort() => _state = WebSocketState.Aborted;
            public override void Dispose() { }
        }

        private sealed class StubCommandFactory : SocketCommandFactory
        {
            public override AbstractSocketCommand CreateCommand(SocketCommandInfo commandInfo, IServiceScope scope)
                => throw new NotSupportedException("These tests never send a real command.");
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
