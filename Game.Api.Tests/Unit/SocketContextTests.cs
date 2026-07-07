using Game.Abstractions.DataAccess;
using Game.Api;
using Game.Api.Services;
using Game.Api.Sockets;
using Game.Core.Players;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.WebSockets;
using System.Text;
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
        public async Task Close_ErrorReason_SendsCloseFrameWithMatchingStatusCode()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new GatedCloseWebSocket();
            var context = CreateContext(socket);

            var closeTask = context.Close(ESocketCloseReason.MessageTooBig, cancellationToken);
            await socket.CloseStarted.WaitAsync(WaitTimeout, cancellationToken);
            socket.CompleteClose();
            await closeTask.WaitAsync(WaitTimeout, cancellationToken);

            // The close frame carries the error status code, not a hardcoded NormalClosure (#610).
            Assert.Equal(WebSocketCloseStatus.MessageTooBig, socket.CloseStatusUsed);
        }

        [Fact]
        public async Task Close_GracefulReason_SendsCloseFrameWithNormalClosure()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new GatedCloseWebSocket();
            var context = CreateContext(socket);

            var closeTask = context.Close(ESocketCloseReason.Finished, cancellationToken);
            await socket.CloseStarted.WaitAsync(WaitTimeout, cancellationToken);
            socket.CompleteClose();
            await closeTask.WaitAsync(WaitTimeout, cancellationToken);

            Assert.Equal(WebSocketCloseStatus.NormalClosure, socket.CloseStatusUsed);
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

        [Fact]
        public async Task SendData_MidFrameCancellation_CompletesTheFrameRatherThanLeavingItHalfSent()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new ScriptedWebSocket();
            var context = CreateContext(socket);

            // A payload larger than one frame, so it is sent as multiple chunks under the send lock.
            var payload = new string('x', SocketContext.MAX_MESSAGE_SIZE + 1000);
            using var cts = new CancellationTokenSource();
            // Cancel the moment the first (non-final) chunk has gone out — mid-frame.
            socket.AfterSendChunk = isFinal =>
            {
                if (!isFinal)
                {
                    cts.Cancel();
                }
            };

            var sent = await context.SendData(payload, cts.Token).WaitAsync(WaitTimeout, cancellationToken);

            // The frame is written to completion despite the mid-frame cancellation, so the next writer can't
            // interleave into a half-sent frame.
            Assert.True(sent);
            Assert.True(socket.SentFrameEndFlags is [false, true]);
            Assert.Equal(payload, Assert.Single(socket.SentMessages));
        }

        [Fact]
        public async Task SendData_CancelledBeforeAcquiringSendLock_ReturnsFalseAndSendsNothing()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new ScriptedWebSocket();
            var context = CreateContext(socket);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // The cancellation is handled locally (returns false), not allowed to escape as an exception.
            var sent = await context.SendData("hello", cts.Token).WaitAsync(WaitTimeout, cancellationToken);

            Assert.False(sent);
            Assert.Empty(socket.SentMessages);
        }

        [Fact]
        public async Task ReadMessage_ReassemblesMultiFrameTextMessage()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new ScriptedWebSocket();
            var context = CreateContext(socket);

            socket.QueueData("hel", endOfMessage: false);
            socket.QueueData("lo", endOfMessage: true);

            var message = await context.ReadMessage(cancellationToken).WaitAsync(WaitTimeout, cancellationToken);

            Assert.Equal("hello", message);
        }

        [Fact]
        public async Task ReadMessage_MultiByteCharacterSplitAcrossFrames_DecodesCorrectly()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new ScriptedWebSocket();
            var context = CreateContext(socket);

            // "hel😀lo" — the emoji's 4-byte UTF-8 sequence (F0 9F 98 80) is split mid-character across two
            // receive fills, reproducing a code point straddling the 4KB buffer boundary (#1699).
            var fullBytes = Encoding.UTF8.GetBytes("hel😀lo");
            var firstChunk = fullBytes[..5];
            var secondChunk = fullBytes[5..];
            socket.QueueBytes(firstChunk, endOfMessage: false);
            socket.QueueBytes(secondChunk, endOfMessage: true);

            var message = await context.ReadMessage(cancellationToken).WaitAsync(WaitTimeout, cancellationToken);

            Assert.Equal("hel😀lo", message);
        }

        [Fact]
        public async Task ReadMessage_CloseFrame_StopsReadingAndReturnsEmpty()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new ScriptedWebSocket();
            var context = CreateContext(socket);

            socket.QueueClose();

            // The Close frame is honoured (the read returns promptly instead of looping), leaving the socket in
            // CloseReceived for the read loop to complete the handshake.
            var message = await context.ReadMessage(cancellationToken).WaitAsync(WaitTimeout, cancellationToken);

            Assert.Equal("", message);
            Assert.Equal(WebSocketState.CloseReceived, socket.State);
        }

        [Fact]
        public async Task ReadMessage_ZeroLengthFrameFlood_IsBoundedAndClosesAsMessageTooBig()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new ScriptedWebSocket();
            var context = CreateContext(socket);

            // A malformed peer streaming endless zero-length continuation frames must not spin forever: every
            // frame counts toward the cap, so the read trips the size limit and closes.
            socket.StreamZeroLengthFramesForever();

            await Assert.ThrowsAsync<WebSocketException>(
                () => context.ReadMessage(cancellationToken).WaitAsync(WaitTimeout, cancellationToken));

            Assert.True(socket.CloseAsyncCalled);
            Assert.Equal(WebSocketCloseStatus.MessageTooBig, socket.CloseStatusUsed);
        }

        private static SocketContext CreateContext(WebSocket socket, bool isAdmin = false)
        {
            var session = new SessionService(new NoOpSessionStore());
            return new SocketContext(socket, playerId: 1, session, isAdmin, NullLogger<SocketContext>.Instance);
        }

        /// <summary>
        /// A <see cref="WebSocket"/> stand-in that scripts receives (data / close / a zero-length flood) and
        /// records outbound frame chunks, so the read-message and send-atomicity contracts can be exercised
        /// deterministically.
        /// </summary>
        private sealed class ScriptedWebSocket : WebSocket
        {
            private readonly Queue<ReceiveStep> _receives = new();
            private bool _floodZeroLengthFrames;
            private readonly List<byte> _pendingSend = [];
            private readonly List<string> _sentMessages = [];
            private WebSocketState _state = WebSocketState.Open;

            /// <summary>The endOfMessage flag of each sent chunk, in order.</summary>
            public List<bool> SentFrameEndFlags { get; } = [];
            public IReadOnlyList<string> SentMessages => _sentMessages;
            public bool CloseAsyncCalled { get; private set; }
            public WebSocketCloseStatus? CloseStatusUsed { get; private set; }

            /// <summary>Invoked after each chunk is sent, with its endOfMessage flag.</summary>
            public Action<bool>? AfterSendChunk { get; set; }

            public void QueueData(string text, bool endOfMessage)
                => _receives.Enqueue(new ReceiveStep(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, endOfMessage));

            public void QueueBytes(byte[] payload, bool endOfMessage)
                => _receives.Enqueue(new ReceiveStep(payload, WebSocketMessageType.Text, endOfMessage));

            public void QueueClose()
                => _receives.Enqueue(new ReceiveStep([], WebSocketMessageType.Close, EndOfMessage: true));

            public void StreamZeroLengthFramesForever() => _floodZeroLengthFrames = true;

            public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_receives.Count == 0 && _floodZeroLengthFrames)
                {
                    return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Text, endOfMessage: false));
                }

                var step = _receives.Dequeue();
                if (step.Payload.Length > 0)
                {
                    Array.Copy(step.Payload, 0, buffer.Array!, buffer.Offset, step.Payload.Length);
                }

                if (step.Type is WebSocketMessageType.Close)
                {
                    _state = WebSocketState.CloseReceived;
                }

                return Task.FromResult(new WebSocketReceiveResult(step.Payload.Length, step.Type, step.EndOfMessage));
            }

            public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            {
                await Task.Yield();
                _pendingSend.AddRange(buffer.ToArray());
                SentFrameEndFlags.Add(endOfMessage);
                if (endOfMessage)
                {
                    _sentMessages.Add(Encoding.UTF8.GetString(_pendingSend.ToArray()));
                    _pendingSend.Clear();
                }

                AfterSendChunk?.Invoke(endOfMessage);
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
            public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
            public override void Abort() { }
            public override void Dispose() { }

            private sealed record ReceiveStep(byte[] Payload, WebSocketMessageType Type, bool EndOfMessage);
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
            public WebSocketCloseStatus? CloseStatusUsed { get; private set; }
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
                CloseStatusUsed = closeStatus;
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
            public Task<PlayerState?> GetSession(int userId, CancellationToken cancellationToken = default) => Task.FromResult<PlayerState?>(null);
            public void Update(PlayerState sessionData, int playerId) { }
            public void Clear(int userId) { }
        }
    }
}
