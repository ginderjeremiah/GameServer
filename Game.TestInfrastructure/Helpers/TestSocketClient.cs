using Game.Api.Models.Common;
using Game.Api.Sockets.Commands;
using Game.Core;
using System.Net.WebSockets;
using System.Text;

namespace Game.TestInfrastructure.Helpers
{
    public class TestSocketClient : IAsyncDisposable
    {
        private WebSocket _socket = null!;
        private ClientWebSocket? _ownedSocket;
        private readonly CancellationTokenSource _cts = new(TimeSpan.FromSeconds(30));

        public WebSocketState State => _socket.State;

        /// <summary>
        /// Connect using a real ClientWebSocket (for real server endpoints). The handshake reads the
        /// selected player from the token claim, so a post-selection token is minted carrying
        /// <paramref name="playerId"/> (defaulting to <paramref name="userId"/>, which the single-seed
        /// integration tests share after an identity reset).
        /// </summary>
        public async Task ConnectAsync(string baseUrl, int userId, int? playerId = null)
        {
            var tokenString = TestAuthHelper.CreateAccessToken(userId, playerId ?? userId);

            _ownedSocket = new ClientWebSocket();

            var wsBase = baseUrl.Replace("https://", "wss://").Replace("http://", "ws://");
            var wsUri = new Uri($"{wsBase}/socket?access_token={Uri.EscapeDataString(tokenString)}");
            await _ownedSocket.ConnectAsync(wsUri, _cts.Token);
            _socket = _ownedSocket;
        }

        /// <summary>
        /// Connect using an in-memory TestServer WebSocket client.
        /// The WebSocketClient from TestServer handles the in-memory transport. The handshake reads the
        /// selected player from the token claim, so a post-selection token is minted carrying
        /// <paramref name="playerId"/> (defaulting to <paramref name="userId"/>, which the single-seed
        /// integration tests share after an identity reset). <paramref name="roles"/> lets a test connect
        /// as an admin (e.g. to exercise <c>SocketContext.IsAdmin</c>-gated behavior).
        /// </summary>
        public async Task ConnectAsync(Microsoft.AspNetCore.TestHost.WebSocketClient wsClient, int userId, int? playerId = null, params string[] roles)
        {
            var tokenString = TestAuthHelper.CreateAccessToken(userId, playerId ?? userId, roles);

            var wsUri = new Uri($"ws://localhost/socket?access_token={Uri.EscapeDataString(tokenString)}");
            _socket = await wsClient.ConnectAsync(wsUri, _cts.Token);
        }

        public async Task<ApiSocketResponse<TResponse>> SendCommandAsync<TResponse>(
            string commandName,
            object? parameters = null)
        {
            var id = await SendCommandNoWaitAsync(commandName, parameters);
            return await ReadUntilAsync(m => m.Deserialize<ApiSocketResponse<TResponse>>(), r => r.Id == id);
        }

        public async Task<ApiSocketResponse> SendCommandRawAsync(string commandName, object? parameters = null)
        {
            var id = await SendCommandNoWaitAsync(commandName, parameters);
            return await ReadUntilAsync(m => m.Deserialize<ApiSocketResponse>(), r => r.Id == id);
        }

        /// <summary>
        /// Sends a command without reading its response and returns the generated command id. Use when the
        /// test cares about a later server-pushed message rather than the command's own reply, so that
        /// reply isn't consumed by an Id match (which would race the push). Also the single build-and-send
        /// path the waiting send overloads delegate to.
        /// </summary>
        public async Task<string> SendCommandNoWaitAsync(string commandName, object? parameters = null)
        {
            var id = Guid.NewGuid().ToString();
            var commandInfo = new SocketCommandInfo(commandName)
            {
                Id = id,
                Parameters = parameters?.Serialize(),
            };

            var bytes = Encoding.UTF8.GetBytes(commandInfo.Serialize());
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);
            return id;
        }

        /// <summary>
        /// Reads until a message whose <c>Name</c> matches arrives (skipping any others, e.g. a different
        /// command's response), for server-pushed commands that carry no request Id to match on. Returns
        /// the push with its typed data so the payload can be asserted.
        /// </summary>
        public Task<ApiSocketResponse<TResponse>> WaitForCommandAsync<TResponse>(string commandName)
        {
            return ReadUntilAsync(m => m.Deserialize<ApiSocketResponse<TResponse>>(), r => r.Name == commandName);
        }

        /// <summary>
        /// Reads from the socket until a response with the given ID arrives, without sending anything.
        /// Use this when a command is emitted through a server-side path (e.g. pub/sub) rather than
        /// directly from the client.
        /// </summary>
        public Task<ApiSocketResponse> WaitForResponseAsync(string? commandId)
        {
            return ReadUntilAsync(m => m.Deserialize<ApiSocketResponse>(), r => r.Id == commandId);
        }

        /// <summary>
        /// Reads the next frame — expected to be a server-initiated close (e.g. right after a push like
        /// <c>SocketReplaced</c> that closes the connection) — and completes the closing handshake so
        /// <see cref="State"/> settles at <see cref="WebSocketState.Closed"/> rather than lingering in
        /// <see cref="WebSocketState.CloseReceived"/>. Returns the status/description the server sent.
        /// </summary>
        public async Task<(WebSocketCloseStatus? Status, string? Description)> WaitForCloseAsync()
        {
            var buffer = new byte[4096];
            await _socket.ReceiveAsync(buffer, _cts.Token);

            if (_socket.State == WebSocketState.CloseReceived)
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, _cts.Token);
            }

            return (_socket.CloseStatus, _socket.CloseStatusDescription);
        }

        public async Task CloseAsync()
        {
            if (_socket.State == WebSocketState.Open)
            {
                try
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", _cts.Token);
                }
                catch
                {
                    // Ignore any errors that occur while closing the socket
                }
            }
        }

        /// <summary>
        /// Reads messages — answering server pings transparently — until one deserializes (via
        /// <paramref name="deserialize"/>) and satisfies <paramref name="match"/>. The single ping-aware
        /// read loop the typed/raw/by-name reads all share, differing only in their deserialize target
        /// and match predicate.
        /// </summary>
        private async Task<T> ReadUntilAsync<T>(Func<string, T?> deserialize, Func<T, bool> match)
            where T : ApiSocketResponse
        {
            while (true)
            {
                var message = await ReadMessageAsync();
                if (message == "ping")
                {
                    await SendPongAsync();
                    continue;
                }

                var response = deserialize(message);
                if (response is not null && match(response))
                {
                    return response;
                }
            }
        }

        private async Task<string> ReadMessageAsync()
        {
            var buffer = new byte[4096];
            using var message = new MemoryStream();
            WebSocketReceiveResult result;
            // Reassemble the whole message: a single logical frame is delivered in buffer-sized chunks, so a
            // payload larger than the buffer (a growing reference-data set) spans multiple reads until EndOfMessage.
            do
            {
                result = await _socket.ReceiveAsync(buffer, _cts.Token);
                message.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
        }

        private async Task SendPongAsync()
        {
            var bytes = Encoding.UTF8.GetBytes("pong");
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);
        }

        public async ValueTask DisposeAsync()
        {
            _socket?.Dispose();
            _cts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
