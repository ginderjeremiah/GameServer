using System.Net.WebSockets;
using System.Text;

namespace Game.TestInfrastructure.Helpers
{
    /// <summary>
    /// A minimal in-memory <see cref="WebSocket"/> for deterministically exercising per-socket send
    /// serialization. <see cref="SendAsync"/> records the peak number of overlapping calls — so a test can
    /// assert sends never run concurrently (<c>WebSocket.SendAsync</c> forbids that) — and, when given a
    /// gate, parks inside the send until the gate is released, so a test can hold one command in its
    /// critical section while probing another. The real in-memory test-host transport tolerates overlapping
    /// sends, so it cannot surface the race this fake is built to catch.
    /// </summary>
    public sealed class FakeWebSocket : WebSocket
    {
        private readonly Task? _sendGate;
        private readonly TimeSpan _sendDuration;
        private readonly TaskCompletionSource _firstSendStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _abortCts = new();
        private readonly object _lock = new();
        private readonly List<byte> _pendingMessage = [];
        private readonly List<string> _sentMessages = [];
        private int _activeSends;

        /// <summary>Completes once the first <see cref="SendAsync"/> call has begun.</summary>
        public Task FirstSendStarted => _firstSendStarted.Task;

        /// <summary>The greatest number of <see cref="SendAsync"/> calls observed in flight at once.</summary>
        public int MaxConcurrentSends { get; private set; }

        /// <summary>The total number of completed sends.</summary>
        public int CompletedSends { get; private set; }

        /// <summary>Whether <see cref="Abort"/> has been called.</summary>
        public bool AbortCalled { get; private set; }

        /// <summary>Whether <see cref="CloseAsync"/> has been called.</summary>
        public bool CloseAsyncCalled { get; private set; }

        /// <summary>The status passed to the most recent <see cref="CloseAsync"/> call.</summary>
        public WebSocketCloseStatus? CloseStatusUsed { get; private set; }

        /// <summary>The fully-sent messages, reassembled from their chunks (one entry per end-of-message).</summary>
        public IReadOnlyList<string> SentMessages
        {
            get
            {
                lock (_lock)
                {
                    return _sentMessages.ToList();
                }
            }
        }

        /// <param name="sendGate">When supplied, each send awaits this task before completing, parking the
        /// caller inside the send. When null, each send instead lingers for <paramref name="sendDuration"/>
        /// so genuinely concurrent callers actually overlap.</param>
        /// <param name="sendDuration">How long an ungated send lingers (default 25ms).</param>
        public FakeWebSocket(Task? sendGate = null, TimeSpan? sendDuration = null)
        {
            _sendGate = sendGate;
            _sendDuration = sendDuration ?? TimeSpan.FromMilliseconds(25);
        }

        public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            var chunk = buffer.ToArray();
            lock (_lock)
            {
                _activeSends++;
                MaxConcurrentSends = Math.Max(MaxConcurrentSends, _activeSends);
            }

            _firstSendStarted.TrySetResult();
            try
            {
                if (_sendGate is not null)
                {
                    // Races the gate against Abort() so a test can simulate a wedged send (a peer with a zero
                    // TCP receive window) that only Abort() — not cancellation — can unblock, mirroring the
                    // real WebSocket contract SocketContext.SendData relies on (#1760).
                    var abortTask = Task.Delay(Timeout.InfiniteTimeSpan, _abortCts.Token);
                    if (await Task.WhenAny(_sendGate, abortTask) == abortTask)
                    {
                        throw new WebSocketException(WebSocketError.InvalidState, "Aborted");
                    }
                }
                else
                {
                    await Task.Delay(_sendDuration, cancellationToken);
                }
            }
            finally
            {
                lock (_lock)
                {
                    _activeSends--;
                }
            }

            lock (_lock)
            {
                CompletedSends++;
                _pendingMessage.AddRange(chunk);
                if (endOfMessage)
                {
                    _sentMessages.Add(Encoding.UTF8.GetString(_pendingMessage.ToArray()));
                    _pendingMessage.Clear();
                }
            }
        }

        public override WebSocketState State => AbortCalled ? WebSocketState.Aborted : CloseAsyncCalled ? WebSocketState.Closed : WebSocketState.Open;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override string? SubProtocol => null;
        public override void Abort()
        {
            AbortCalled = true;
            _abortCts.Cancel();
        }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        {
            CloseAsyncCalled = true;
            CloseStatusUsed = closeStatus;
            return Task.CompletedTask;
        }
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose() { }

        // These tests drive ExecuteCommand/SendData directly rather than the read loop, so receives never
        // happen — return a task that never completes.
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            => new TaskCompletionSource<WebSocketReceiveResult>().Task;
    }
}
