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
        private const short MAX_MESSAGE_SIZE = 1024 * 4;
        private readonly byte[] _buffer = new byte[MAX_MESSAGE_SIZE];

        private readonly TaskCompletionSource<ESocketCloseReason> _socketClosedSource = new();
        private readonly ILogger<SocketContext> _logger;
        private readonly WebSocket _socket;

        // Guards every WebSocket output operation (sends and the close frame), which forbid overlapping
        // calls. The read loop, the pub/sub processor, and the inactivity/close paths can all reach these
        // concurrently; serializing here keeps two outputs from throwing or interleaving a multi-chunk
        // frame. Distinct from SocketHandler's command lock because ExecuteCommand holds that lock while
        // calling SendData. Left undisposed for the same reason as the command lock.
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public string SocketId { get; }
        public int PlayerId { get; }
        public SessionService Session { get; }
        public WebSocketState State => _socket.State;

        public SocketContext(WebSocket socket, int playerId, SessionService session, ILogger<SocketContext> logger)
        {
            _socket = socket;
            SocketId = Guid.NewGuid().ToString();
            PlayerId = playerId;
            Session = session;
            _logger = logger;
        }

        public async Task<bool> SendData(string data, CancellationToken cancellationToken = default)
        {
            if (_socket.State is not Open)
            {
                return false;
            }

            _logger.LogDebug("Sending data to playerId ({PlayerId}) from socket ({Id}): {Data}", PlayerId, SocketId, data);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                for (int i = 0; i < dataBytes.Length; i += MAX_MESSAGE_SIZE)
                {
                    if (dataBytes.Length - i <= MAX_MESSAGE_SIZE)
                    {
                        await _socket.SendAsync(
                            buffer: new ArraySegment<byte>(dataBytes, i, dataBytes.Length - i),
                            messageType: WebSocketMessageType.Text,
                            endOfMessage: true,
                            cancellationToken
                        );
                    }
                    else
                    {
                        await _socket.SendAsync(
                            buffer: new ArraySegment<byte>(dataBytes, i, MAX_MESSAGE_SIZE),
                            messageType: WebSocketMessageType.Text,
                            endOfMessage: false,
                            cancellationToken
                        );
                    }
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
            WebSocketReceiveResult result;
            var buffersRead = 0;
            do
            {
                result = await _socket.ReceiveAsync(new ArraySegment<byte>(_buffer), cancellationToken);
                if (result.Count > 0)
                {
                    message.Append(Encoding.UTF8.GetString(_buffer, 0, result.Count));
                    buffersRead++;
                }
            }
            while (!result.EndOfMessage && buffersRead < 1024);

            if (buffersRead >= 1024)
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
                    await _sendLock.WaitAsync(cancellationToken);
                    try
                    {
                        if (_socket.State is WebSocketState.Open)
                        {
                            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, closeReason.GetDescription(), cancellationToken);
                        }
                    }
                    finally
                    {
                        _sendLock.Release();
                    }
                }
            }
            finally
            {
                // Settle WaitSocketClosed only after the close frame has been sent, so the middleware that
                // awaits it can't dispose the socket and race the flush. Best-effort: the finally still
                // releases waiters if CloseAsync throws or the drain token cancels the send.
                _socketClosedSource.TrySetResult(closeReason);
            }
        }
    }
}
