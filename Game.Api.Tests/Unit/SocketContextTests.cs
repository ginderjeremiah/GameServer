using Game.Abstractions.DataAccess;
using Game.Api;
using Game.Api.Services;
using Game.Api.Sockets;
using Game.Core.Players;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.WebSockets;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Guards the close-frame ordering contract: <see cref="SocketContext.WaitSocketClosed"/> must settle only
    /// after the close frame has actually been sent, so the middleware that resumes on it can't dispose the
    /// underlying WebSocket while the frame is still flushing (#605). The settle must still be best-effort —
    /// waiters are released even when the close frame send faults or is cancelled.
    /// </summary>
    public class SocketContextTests
    {
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        [Fact]
        public async Task Close_DoesNotSettleWaitSocketClosed_UntilCloseFrameHasBeenSent()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new GatedCloseWebSocket();
            var context = CreateContext(socket);

            var waitClosed = context.WaitSocketClosed();
            var closeTask = context.Close(ESocketCloseReason.Inactivity, cancellationToken);

            // CloseAsync is in flight (gated); the waiter must not be released yet, otherwise the middleware
            // could dispose the socket before the frame flushes.
            await socket.CloseStarted.WaitAsync(WaitTimeout, cancellationToken);
            Assert.False(waitClosed.IsCompleted);

            // Letting the close frame finish flushing is what releases the waiter.
            socket.CompleteClose();
            await closeTask.WaitAsync(WaitTimeout, cancellationToken);
            await waitClosed.WaitAsync(WaitTimeout, cancellationToken);
            Assert.True(waitClosed.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task Close_StillSettlesWaitSocketClosed_WhenCloseFrameSendThrows()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new GatedCloseWebSocket();
            var context = CreateContext(socket);

            var waitClosed = context.WaitSocketClosed();
            var closeTask = context.Close(ESocketCloseReason.Inactivity, cancellationToken);
            await socket.CloseStarted.WaitAsync(WaitTimeout, cancellationToken);

            socket.FailClose(new WebSocketException("close frame send failed"));

            // The send fault propagates out of Close, but the waiter is still released (best-effort).
            await Assert.ThrowsAsync<WebSocketException>(() => closeTask);
            await waitClosed.WaitAsync(WaitTimeout, cancellationToken);
            Assert.True(waitClosed.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task Close_WhenSocketNotOpen_SettlesWithoutSendingCloseFrame()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new GatedCloseWebSocket { StateOverride = WebSocketState.Closed };
            var context = CreateContext(socket);

            await context.Close(ESocketCloseReason.Finished, cancellationToken).WaitAsync(WaitTimeout, cancellationToken);

            Assert.False(socket.CloseAsyncCalled);
            await context.WaitSocketClosed().WaitAsync(WaitTimeout, cancellationToken);
        }

        private static SocketContext CreateContext(WebSocket socket)
        {
            var session = new SessionService(new NoOpSessionStore());
            return new SocketContext(socket, playerId: 1, session, NullLogger<SocketContext>.Instance);
        }

        /// <summary>
        /// A <see cref="WebSocket"/> stand-in whose <see cref="CloseAsync"/> blocks until the test releases it,
        /// so a test can observe whether the close frame has finished sending before the waiter is settled.
        /// </summary>
        private sealed class GatedCloseWebSocket : WebSocket
        {
            private readonly TaskCompletionSource _closeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource _closeGate = new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>Completes once <see cref="CloseAsync"/> has begun (and is blocking on the gate).</summary>
            public Task CloseStarted => _closeStarted.Task;
            public bool CloseAsyncCalled { get; private set; }
            public WebSocketState StateOverride { get; init; } = WebSocketState.Open;

            public void CompleteClose() => _closeGate.TrySetResult();
            public void FailClose(Exception ex) => _closeGate.TrySetException(ex);

            public override WebSocketState State => StateOverride;
            public override WebSocketCloseStatus? CloseStatus => null;
            public override string? CloseStatusDescription => null;
            public override string? SubProtocol => null;

            public override async Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            {
                CloseAsyncCalled = true;
                _closeStarted.TrySetResult();
                await _closeGate.Task;
            }

            public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();
            public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => Task.CompletedTask;
            public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
            public override void Abort() { }
            public override void Dispose() { }
        }

        private sealed class NoOpSessionStore : ISessionStore
        {
            public Task<PlayerState?> GetSession(int userId) => Task.FromResult<PlayerState?>(null);
            public void Update(PlayerState sessionData, int playerId) { }
            public void Clear(int userId) { }
        }
    }
}
