using Game.Api.Models.Common;
using Game.Api.Services;
using Game.Core;
using System.Net.WebSockets;
using System.Text;
using static System.Net.WebSockets.WebSocketState;

namespace Game.Api.Sockets
{
    public class SocketContext
    {
        internal const short MAX_MESSAGE_SIZE = 1024 * 4;

        // Caps a single inbound message at MAX_FRAMES_PER_MESSAGE * MAX_MESSAGE_SIZE (~4 MB). Every received
        // frame counts toward it — including empty ones — so a peer streaming zero-length frames is bounded
        // rather than spinning the read loop until cancellation.
        private const int MAX_FRAMES_PER_MESSAGE = 1024;

        private readonly byte[] _buffer = new byte[MAX_MESSAGE_SIZE];

        // Sized for the worst case (every byte a standalone char, plus a possible carried-over decoder char)
        // so a single ReceiveAsync fill can never overflow it, regardless of how the UTF-8 sequences in that
        // fill are split.
        private readonly char[] _charBuffer = new char[Encoding.UTF8.GetMaxCharCount(MAX_MESSAGE_SIZE)];

        private readonly TaskCompletionSource<ESocketCloseReason> _socketClosedSource = new();
        private readonly ILogger<SocketContext> _logger;
        private readonly WebSocket _socket;

        // Guards every WebSocket output operation (sends and the close frame), which forbid overlapping
        // calls. The read loop, the pub/sub processor, and the inactivity/close paths can all reach these
        // concurrently; serializing here keeps two outputs from throwing or interleaving a multi-chunk
        // frame. Distinct from SocketHandler's command lock because ExecuteCommand holds that lock while
        // calling SendData. Left undisposed for the same reason as the command lock.
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        // Upper bound Close applies — on top of the caller's cancellationToken — to acquiring the send lock
        // and completing the close handshake, before forcibly aborting the underlying socket. A wedged send
        // (e.g. a client stalled with a TCP zero-window) holds the send lock and deliberately ignores its own
        // cancellation once a frame has begun (see SendData's per-chunk CancellationToken.None), so cancelling
        // the *wait* for the lock doesn't unblock the send itself — only Abort() does. This bound is what lets
        // the inactivity watchdog's terminal Close (which passes no caller token at all) and the shutdown
        // drain's deadline-bound Close both guarantee they eventually complete rather than hanging forever
        // (#1726).
        private static readonly TimeSpan DefaultCloseAbortTimeout = TimeSpan.FromSeconds(5);

        private readonly TimeSpan _closeAbortTimeout;

        public string SocketId { get; }
        public int PlayerId { get; }
        public SessionService Session { get; }
        public WebSocketState State => _socket.State;

        /// <summary>
        /// Whether the connected user holds the <see cref="ERole.Admin"/> role, read once from the
        /// token that authenticated this connection's handshake. Reference-data commands use it to
        /// decide whether authoring-only fields (e.g. <c>DesignerNotes</c>) belong in the response — see
        /// <see cref="Commands.AbstractReferenceDataCommand{TModel}"/>. Fixed for the connection's
        /// lifetime, matching every other claim the handshake token carries (a role change takes effect
        /// on the next reconnect, not mid-connection).
        /// </summary>
        public bool IsAdmin { get; }

        public SocketContext(WebSocket socket, int playerId, SessionService session, bool isAdmin, ILogger<SocketContext> logger, TimeSpan? closeAbortTimeout = null)
        {
            _socket = socket;
            SocketId = Guid.NewGuid().ToString();
            PlayerId = playerId;
            Session = session;
            IsAdmin = isAdmin;
            _logger = logger;
            _closeAbortTimeout = closeAbortTimeout ?? DefaultCloseAbortTimeout;
        }

        public async Task<bool> SendData(string data, CancellationToken cancellationToken = default)
        {
            if (_socket.State is not Open)
            {
                return false;
            }

            _logger.LogDebug("Sending data to playerId ({PlayerId}) from socket ({Id}): {Data}", PlayerId, SocketId, data);
            var dataBytes = Encoding.UTF8.GetBytes(data);

            try
            {
                await _sendLock.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Cancelled while waiting for the send lock — no frame was started, so there is nothing to
                // unwind. Report the send as not delivered rather than letting the cancellation escape.
                return false;
            }

            try
            {
                for (int i = 0; i < dataBytes.Length; i += MAX_MESSAGE_SIZE)
                {
                    var isFinalChunk = dataBytes.Length - i <= MAX_MESSAGE_SIZE;
                    var chunkSize = isFinalChunk ? dataBytes.Length - i : MAX_MESSAGE_SIZE;

                    // Send each chunk with CancellationToken.None: once a multi-chunk frame has begun it must
                    // be written to completion under the lock. Cancelling mid-frame would release the lock on a
                    // half-sent frame and let the next writer interleave into it, corrupting the stream.
                    // Cancellation is honoured before the frame starts (the lock wait above), not during it.
                    await _socket.SendAsync(
                        buffer: new ArraySegment<byte>(dataBytes, i, chunkSize),
                        messageType: WebSocketMessageType.Text,
                        endOfMessage: isFinalChunk,
                        CancellationToken.None
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send socket data: {Data}", data);
                return false;
            }
            finally
            {
                _sendLock.Release();
            }

            return true;
        }

        public async Task<bool> SendData<T>(T data, CancellationToken cancellationToken = default) where T : ApiSocketResponse
        {
            return await SendData(data.Serialize<object>(), cancellationToken);
        }

        public async Task<string> ReadMessage(CancellationToken cancellationToken = default)
        {
            var message = new StringBuilder();

            // ReceiveAsync fills at most MAX_MESSAGE_SIZE bytes per call regardless of the sender's UTF-8
            // framing, so a multi-byte code point can land split across two fills. A stateful decoder carries
            // an incomplete trailing sequence over to the next GetChars call instead of decoding each fill in
            // isolation, which would otherwise turn each half into a replacement character (U+FFFD).
            var decoder = Encoding.UTF8.GetDecoder();
            WebSocketReceiveResult result;
            var framesRead = 0;
            do
            {
                result = await _socket.ReceiveAsync(new ArraySegment<byte>(_buffer), cancellationToken);

                // A Close frame ends the message stream; stop reading and let the read loop run the closing
                // handshake off the CloseReceived state. It carries no data, so don't append or count it.
                if (result.MessageType is WebSocketMessageType.Close)
                {
                    break;
                }

                framesRead++;
                if (result.Count > 0)
                {
                    var charCount = decoder.GetChars(_buffer, 0, result.Count, _charBuffer, 0);
                    message.Append(_charBuffer, 0, charCount);
                }
            }
            while (!result.EndOfMessage && framesRead < MAX_FRAMES_PER_MESSAGE);

            // Only a message that hit the frame cap without completing is over-size; a message that ends
            // exactly on the cap is whole and accepted.
            if (framesRead >= MAX_FRAMES_PER_MESSAGE && !result.EndOfMessage)
            {
                await Close(ESocketCloseReason.MessageTooBig);
                throw new WebSocketException("Socket message exceeded maximum allowed size.");
            }

            return message.ToString();
        }

        public async Task WaitSocketClosed()
        {
            await _socketClosedSource.Task;
        }

        public async Task Close(ESocketCloseReason closeReason = ESocketCloseReason.Finished, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_socket.State is WebSocketState.Open)
                {
                    // The close frame is a send too, so take the same lock to avoid overlapping an in-flight
                    // SendData; re-check state inside the lock so a racing close only sends one close frame.
                    // Bound the wait (and the close handshake itself) with _closeAbortTimeout on top of the
                    // caller's token: a wedged send holding the lock ignores its own cancellation, so falling
                    // back to Abort() here is the only way to guarantee this method — and whatever awaits it —
                    // doesn't hang forever (#1726).
                    using var abortTimeoutCts = new CancellationTokenSource(_closeAbortTimeout);
                    using var boundedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, abortTimeoutCts.Token);

                    var lockAcquired = true;
                    try
                    {
                        await _sendLock.WaitAsync(boundedCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Timed out waiting for the send lock while closing socket ({Id}); aborting the connection.", SocketId);
                        _socket.Abort();
                        lockAcquired = false;
                    }

                    if (lockAcquired)
                    {
                        try
                        {
                            if (_socket.State is WebSocketState.Open)
                            {
                                try
                                {
                                    await _socket.CloseAsync(closeReason.GetCloseStatus(), closeReason.GetDescription(), boundedCts.Token);
                                }
                                catch (OperationCanceledException)
                                {
                                    _logger.LogWarning("Timed out sending the close frame on socket ({Id}); aborting the connection.", SocketId);
                                    _socket.Abort();
                                }
                            }
                        }
                        finally
                        {
                            _sendLock.Release();
                        }
                    }
                }
            }
            finally
            {
                // Settle WaitSocketClosed only after the close frame has been sent (or the connection aborted),
                // so the middleware that awaits it can't dispose the socket and race the flush. Best-effort: the
                // finally still releases waiters if CloseAsync throws or the bound elapses.
                _socketClosedSource.TrySetResult(closeReason);
            }
        }
    }
}
