using Game.Api;
using Game.Api.Services;
using Game.Api.Sockets;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.WebSockets;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Verifies the graceful socket drain that lets a stopping/draining instance close player sockets
    /// cleanly instead of relying on the process being force-killed (#526 — see <c>docs/backend.md</c> →
    /// "Graceful socket drain on shutdown"). A <see cref="DrainableWebSocket"/> stands in for the transport
    /// so both the cooperative-client (close handshake completes) and the unresponsive-client (the bounded
    /// drain window elapses and the blocked receive must be aborted) paths can be driven deterministically;
    /// the supporting services are resolved from the real host.
    /// </summary>
    [Collection("Integration")]
    public class SocketDrainTests : ApiIntegrationTestBase
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);
        private static readonly string RegistryCategory = typeof(SocketConnectionRegistry).FullName!;

        private readonly CapturingLoggerProvider _capturingProvider = new();

        public SocketDrainTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        protected override GameServerFactory CreateFactory(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
        {
            return new GameServerFactory(containers, testOutputHelper, [_capturingProvider]);
        }

        [Fact]
        public async Task DrainAsync_CooperativeClient_ClosesSocketWithServerShuttingDownReason()
        {
            using var scope = CreateScope();
            var (socket, handler) = CreateHandler(scope, echoServerClose: true);
            var registry = CreateRegistry(scope, TimeSpan.FromSeconds(30));

            registry.Register(handler);
            await socket.ReceiveStarted.WaitAsync(WaitTimeout, CancellationToken);

            await registry.DrainAsync().WaitAsync(WaitTimeout, CancellationToken);

            // A clean close frame is sent with the shutting-down reason, and both loops wound down.
            Assert.Equal(WebSocketCloseStatus.NormalClosure, socket.SentCloseStatus);
            Assert.NotNull(socket.SentCloseDescription);
            Assert.Contains("shutting down", socket.SentCloseDescription, StringComparison.OrdinalIgnoreCase);
            Assert.True(handler.Completion.IsCompletedSuccessfully);

            // The cooperative close completed within the window, so the drain never reached its abort path.
            Assert.DoesNotContain(
                _capturingProvider.Entries,
                e => e.Category == RegistryCategory && e.Level == LogLevel.Warning);
        }

        [Fact]
        public async Task DrainAsync_ClientNeverCompletesHandshake_AbortsAfterTimeoutInsteadOfHanging()
        {
            using var scope = CreateScope();
            var (socket, handler) = CreateHandler(scope, echoServerClose: false);
            var registry = CreateRegistry(scope, TimeSpan.FromMilliseconds(300));

            registry.Register(handler);
            await socket.ReceiveStarted.WaitAsync(WaitTimeout, CancellationToken);

            // Without the bounded-drain abort the read loop's receive would block forever; the drain
            // completing at all is what proves the abort unwedged it.
            await registry.DrainAsync().WaitAsync(WaitTimeout, CancellationToken);

            Assert.Equal(WebSocketCloseStatus.NormalClosure, socket.SentCloseStatus);
            Assert.True(handler.Completion.IsCompleted);
            Assert.Contains(
                _capturingProvider.Entries,
                e => e.Category == RegistryCategory && e.Level == LogLevel.Warning && e.Message.Contains("did not complete"));
        }

        [Fact]
        public async Task RegisterSocket_ThenDrain_ClosesSocketThroughTheRealServiceWiring()
        {
            // Drive the real RegisterSocket → registry path (Redis presence, pub/sub subscribe, the singleton
            // registry) so the production wiring is covered, but with a DrainableWebSocket rather than the
            // in-memory transport — whose close handshake races the middleware's socket disposal and so can't
            // surface this behaviour deterministically.
            using var scope = CreateScope();
            var session = scope.ServiceProvider.GetRequiredService<SessionService>();
            session.CreateSession(userId: 4242, playerId: 4242);
            var socketManager = scope.ServiceProvider.GetRequiredService<SocketManagerService>();
            var registry = Factory.Services.GetRequiredService<SocketConnectionRegistry>();

            var socket = new DrainableWebSocket(echoServerClose: true);
            var context = await socketManager.RegisterSocket(socket, session);
            await socket.ReceiveStarted.WaitAsync(WaitTimeout, CancellationToken);

            await registry.DrainAsync().WaitAsync(WaitTimeout, CancellationToken);

            Assert.Equal(WebSocketCloseStatus.NormalClosure, socket.SentCloseStatus);
            Assert.NotNull(socket.SentCloseDescription);
            Assert.Contains("shutting down", socket.SentCloseDescription, StringComparison.OrdinalIgnoreCase);

            await socketManager.UnRegisterSocket(context);
        }

        [Fact]
        public async Task DrainAsync_NoLiveSockets_CompletesWithoutError()
        {
            using var scope = CreateScope();
            var registry = CreateRegistry(scope, TimeSpan.FromSeconds(5));

            await registry.DrainAsync().WaitAsync(WaitTimeout, CancellationToken);

            Assert.DoesNotContain(
                _capturingProvider.Entries,
                e => e.Category == RegistryCategory && e.Level == LogLevel.Warning);
        }

        private (DrainableWebSocket Socket, SocketHandler Handler) CreateHandler(IServiceScope scope, bool echoServerClose)
        {
            var session = scope.ServiceProvider.GetRequiredService<SessionService>();
            var contextLogger = scope.ServiceProvider.GetRequiredService<ILogger<SocketContext>>();
            var handlerLogger = scope.ServiceProvider.GetRequiredService<ILogger<SocketHandler>>();
            var commandFactory = scope.ServiceProvider.GetRequiredService<SocketCommandFactory>();
            var scopeFactory = scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>();

            var socket = new DrainableWebSocket(echoServerClose);
            var context = new SocketContext(socket, playerId: 1, session, contextLogger);
            var handler = new SocketHandler(context, commandFactory, scopeFactory, handlerLogger, () => Task.CompletedTask);
            return (socket, handler);
        }

        private SocketConnectionRegistry CreateRegistry(IServiceScope scope, TimeSpan drainTimeout)
        {
            var lifetime = scope.ServiceProvider.GetRequiredService<IHostApplicationLifetime>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SocketConnectionRegistry>>();
            return new SocketConnectionRegistry(lifetime, logger, drainTimeout);
        }

        /// <summary>
        /// A <see cref="WebSocket"/> stand-in whose blocking receive can be unblocked two ways — by the
        /// server's own close (modelling a cooperative client that completes the handshake) or only by the
        /// drain cancelling the receive token (modelling a client that never echoes the close). It records
        /// the close frame so a test can assert the reason that was sent.
        /// </summary>
        private sealed class DrainableWebSocket(bool echoServerClose) : WebSocket
        {
            private readonly TaskCompletionSource<WebSocketReceiveResult> _receive = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource _receiveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private WebSocketState _state = WebSocketState.Open;

            /// <summary>Completes once the read loop has begun blocking on a receive.</summary>
            public Task ReceiveStarted => _receiveStarted.Task;

            public WebSocketCloseStatus? SentCloseStatus { get; private set; }
            public string? SentCloseDescription { get; private set; }

            public override WebSocketState State => _state;
            public override WebSocketCloseStatus? CloseStatus => SentCloseStatus;
            public override string? CloseStatusDescription => SentCloseDescription;
            public override string? SubProtocol => null;

            public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            {
                _receiveStarted.TrySetResult();
                using var registration = cancellationToken.Register(
                    () => _receive.TrySetException(new OperationCanceledException(cancellationToken)));
                return await _receive.Task;
            }

            public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            {
                SentCloseStatus = closeStatus;
                SentCloseDescription = statusDescription;
                if (echoServerClose)
                {
                    // Cooperative client: the close echo completes the read loop's pending receive.
                    _state = WebSocketState.Closed;
                    _receive.TrySetResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true, closeStatus, statusDescription));
                }
                else
                {
                    // Unresponsive client: the receive stays blocked until the drain aborts its token.
                    _state = WebSocketState.CloseSent;
                }

                return Task.CompletedTask;
            }

            public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => Task.CompletedTask;
            public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
            public override void Abort() => _state = WebSocketState.Aborted;
            public override void Dispose() { }
        }
    }
}
