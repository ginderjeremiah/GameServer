using System.Net.WebSockets;

namespace GameServer.Sockets
{
    public class SocketContext
    {
        private readonly TaskCompletionSource<ESocketCloseReason> _socketClosedSource = new();

        public WebSocket Socket { get; }
        public string SocketId { get; }
        public int PlayerId { get; }

        public SocketContext(WebSocket socket, int playerId)
        {
            Socket = socket;
            SocketId = Guid.NewGuid().ToString();
            PlayerId = playerId;
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
    }
}
