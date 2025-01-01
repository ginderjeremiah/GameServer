using Game.Api.Models.Common;
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

        public WebSocket Socket { get; }
        public string SocketId { get; }
        public int PlayerId { get; }

        public SocketContext(WebSocket socket, int playerId, ILogger<SocketContext> logger)
        {
            Socket = socket;
            SocketId = Guid.NewGuid().ToString();
            PlayerId = playerId;
            _logger = logger;
        }

        public async Task<bool> SendData(string data)
        {
            if (Socket.State is not Open)
            {
                return false;
            }

            _logger.LogDebug("Sending data to playerId ({PlayerId}) from socket ({Id}): {Data}", PlayerId, SocketId, data);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            try
            {
                for (int i = 0; i < dataBytes.Length; i += MAX_MESSAGE_SIZE)
                {
                    if (dataBytes.Length - i <= MAX_MESSAGE_SIZE)
                    {
                        await Socket.SendAsync(new ArraySegment<byte>(dataBytes, i, dataBytes.Length - i), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    else
                    {
                        await Socket.SendAsync(new ArraySegment<byte>(dataBytes, i, MAX_MESSAGE_SIZE), WebSocketMessageType.Text, false, CancellationToken.None);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send socket data: {Data}", data);
                return false;
            }

            return true;
        }

        public async Task<bool> SendData<T>(T data) where T : ApiSocketResponse
        {
            return await SendData(data.Serialize<object>());
        }

        public async Task<string> ReadMessage()
        {
            var message = new StringBuilder();
            WebSocketReceiveResult result;
            do
            {
                result = await Socket.ReceiveAsync(new ArraySegment<byte>(_buffer), CancellationToken.None);
                message.Append(ReadBuffer());
            }
            while (!result.EndOfMessage);

            return message.ToString();
        }

        public async Task WaitSocketClosed()
        {
            await _socketClosedSource.Task;
        }

        public async Task Close(ESocketCloseReason closeReason = ESocketCloseReason.Finished)
        {
            _socketClosedSource.TrySetResult(closeReason);
            if (Socket.State is WebSocketState.Open)
            {
                await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, closeReason.GetDescription(), CancellationToken.None);
            }
        }

        private string ReadBuffer()
        {
            var lastNonZeroByte = -1;
            for (var i = _buffer.Length - 1; i >= 0 && lastNonZeroByte == -1; i--)
            {
                if (_buffer[i] != 0)
                {
                    lastNonZeroByte = i;
                }
            }

            var str = Encoding.UTF8.GetString(_buffer, 0, lastNonZeroByte + 1);
            ClearBuffer(lastNonZeroByte);
            return str;
        }

        private void ClearBuffer(int end = -1)
        {
            var last = end > 0 ? end : _buffer.Length - 1;

            for (var i = 0; i <= last; i++)
            {
                _buffer[i] = 0;
            }
        }
    }
}
