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
        public async Task Close_SendLockHeldByWedgedSend_AbortsInsteadOfHangingForever()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new WedgeableSendWebSocket();
            var context = CreateContext(socket, closeAbortTimeout: TimeSpan.FromMilliseconds(100));

            // Start a send that never completes on its own (mirrors a client stalled with a TCP zero-window),
            // holding the send lock indefinitely.
            var wedgedSend = context.SendData("stuck", CancellationToken.None);
            await socket.SendStarted.WaitAsync(WaitTimeout, cancellationToken);

            // Close cannot acquire the send lock within its bound; it must abort the connection rather than
            // hang forever waiting for a send that will never release the lock on its own (#1726).
            await context.Close(ESocketCloseReason.Inactivity).WaitAsync(WaitTimeout, cancellationToken);

            Assert.True(socket.AbortCalled);
            await context.WaitSocketClosed().WaitAsync(WaitTimeout, cancellationToken);

            // The abort unblocks the wedged send too, since it faults the underlying SendAsync call.
            var sent = await wedgedSend.WaitAsync(WaitTimeout, cancellationToken);
            Assert.False(sent);
        }

        [Fact]
        public async Task Close_CallerTokenCancelsMidWait_AbortsInsteadOfPropagatingCancellation()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new WedgeableSendWebSocket();
            // A generous internal bound, so only the caller's token below can be what trips the fallback.
            var context = CreateContext(socket, closeAbortTimeout: WaitTimeout);

            var wedgedSend = context.SendData("stuck", CancellationToken.None);
            await socket.SendStarted.WaitAsync(WaitTimeout, cancellationToken);

            using var callerCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            // Mirrors SocketHandler.ShutdownAsync passing the shutdown drain's deadline token: once it fires,
            // Close must abort rather than letting the cancellation propagate and leaving the caller to somehow
            // unwedge the connection itself.
            await context.Close(ESocketCloseReason.ServerShuttingDown, callerCts.Token).WaitAsync(WaitTimeout, cancellationToken);

            Assert.True(socket.AbortCalled);
            await context.WaitSocketClosed().WaitAsync(WaitTimeout, cancellationToken);
            await wedgedSend.WaitAsync(WaitTimeout, cancellationToken);
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
        public async Task Close_SocketInCloseReceived_CompletesTheHandshakeWithNormalClosure()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            // Mirrors the read loop's terminal Close after ReceiveAsync returns a client-initiated Close
            // frame: the socket is already in CloseReceived, not Open.
            var socket = new GatedCloseWebSocket { StateOverride = WebSocketState.CloseReceived };
            var context = CreateContext(socket);

            var closeTask = context.Close(ESocketCloseReason.Finished, cancellationToken);
            await socket.CloseStarted.WaitAsync(WaitTimeout, cancellationToken);
            socket.CompleteClose();
            await closeTask.WaitAsync(WaitTimeout, cancellationToken);

            // CloseAsync is called (completing the handshake with a reciprocal close frame) rather than
            // being skipped because the state isn't Open (#1759).
            Assert.True(socket.CloseAsyncCalled);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, socket.CloseStatusUsed);
            await context.WaitSocketClosed().WaitAsync(WaitTimeout, cancellationToken);
        }

        [Fact]
        public async Task SendData_WedgedSend_AbortsInsteadOfHangingForever()
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var socket = new WedgeableSendWebSocket();
            var context = CreateContext(socket, sendAbortTimeout: TimeSpan.FromMilliseconds(100));

            // Mirrors a client stalled with a TCP zero-window: the send never completes on its own, holding
            // the send lock (and, in RunCommandUnderLock's case, the per-socket command lock too) indefinitely
            // unless SendData itself falls back to Abort() the same way Close() already does (#1741) — closing
            // the gap that fix left on this, the far more common send path (#1760).
            var sent = await context.SendData("stuck").WaitAsync(WaitTimeout, cancellationToken);

            Assert.False(sent);
            Assert.True(socket.AbortCalled);
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

        private static SocketContext CreateContext(WebSocket socket, bool isAdmin = false, TimeSpan? closeAbortTimeout = null, TimeSpan? sendAbortTimeout = null)
        {
            var session = new SessionService(new NoOpSessionStore());
            return new SocketContext(socket, playerId: 1, session, isAdmin, NullLogger<SocketContext>.Instance, closeAbortTimeout, sendAbortTimeout);
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

        /// <summary>
        /// A <see cref="WebSocket"/> stand-in whose <see cref="SendAsync"/> never completes on its own —
        /// mirroring a client stalled with a TCP zero-window — and deliberately ignores its cancellation
        /// token, exactly like the mid-frame chunk sends in <see cref="SocketContext.SendData"/> do. Only
        /// <see cref="Abort"/> unblocks it, so a test can prove <see cref="SocketContext.Close"/> falls back
        /// to it instead of hanging forever (#1726).
        /// </summary>
        private sealed class WedgeableSendWebSocket : WebSocket
        {
            private readonly TaskCompletionSource _sendStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource _sendGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private WebSocketState _state = WebSocketState.Open;

            /// <summary>Completes once a <see cref="SendAsync"/> call has begun (and is now wedged).</summary>
            public Task SendStarted => _sendStarted.Task;
            public bool AbortCalled { get; private set; }
            public bool CloseAsyncCalled { get; private set; }

            public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            {
                _sendStarted.TrySetResult();
                await _sendGate.Task;
            }

            public override void Abort()
            {
                AbortCalled = true;
                _state = WebSocketState.Aborted;
                _sendGate.TrySetException(new WebSocketException(WebSocketError.InvalidState, "Aborted"));
            }

            public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            {
                CloseAsyncCalled = true;
                _state = WebSocketState.Closed;
                return Task.CompletedTask;
            }

            public override WebSocketState State => _state;
            public override WebSocketCloseStatus? CloseStatus => null;
            public override string? CloseStatusDescription => null;
            public override string? SubProtocol => null;
            public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
                => new TaskCompletionSource<WebSocketReceiveResult>().Task;
            public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
            public override void Dispose() { }
        }

        private sealed class NoOpSessionStore : ISessionStore
        {
            public Task<PlayerState?> GetSession(int userId, CancellationToken cancellationToken = default) => Task.FromResult<PlayerState?>(null);
            public void Update(PlayerState sessionData, int playerId) { }
            public Task UpdateAsync(PlayerState sessionData, int playerId, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void Clear(int userId) { }
        }
    }
}
