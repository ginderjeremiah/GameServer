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
        /// Connect using a real ClientWebSocket (for real server endpoints).
        /// </summary>
        public async Task ConnectAsync(string baseUrl, int userId)
        {
            var tokenString = TestAuthHelper.CreateAccessToken(userId);

            _ownedSocket = new ClientWebSocket();

            var wsBase = baseUrl.Replace("https://", "wss://").Replace("http://", "ws://");
            var wsUri = new Uri($"{wsBase}/socket?access_token={Uri.EscapeDataString(tokenString)}");
            await _ownedSocket.ConnectAsync(wsUri, _cts.Token);
            _socket = _ownedSocket;
        }

        /// <summary>
        /// Connect using an in-memory TestServer WebSocket client.
        /// The WebSocketClient from TestServer handles the in-memory transport.
        /// </summary>
        public async Task ConnectAsync(Microsoft.AspNetCore.TestHost.WebSocketClient wsClient, int userId)
        {
            var tokenString = TestAuthHelper.CreateAccessToken(userId);

            var wsUri = new Uri($"ws://localhost/socket?access_token={Uri.EscapeDataString(tokenString)}");
            _socket = await wsClient.ConnectAsync(wsUri, _cts.Token);
        }

        public async Task<ApiSocketResponse<TResponse>> SendCommandAsync<TResponse>(
            string commandName,
            object? parameters = null)
        {
            var commandInfo = new SocketCommandInfo(commandName)
            {
                Id = Guid.NewGuid().ToString(),
                Parameters = parameters?.Serialize(),
            };

            var json = commandInfo.Serialize();
            var bytes = Encoding.UTF8.GetBytes(json);
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);

            return await ReadResponseAsync<TResponse>(commandInfo.Id);
        }

        public async Task<ApiSocketResponse> SendCommandRawAsync(string commandName, object? parameters = null)
        {
            var commandInfo = new SocketCommandInfo(commandName)
            {
                Id = Guid.NewGuid().ToString(),
                Parameters = parameters?.Serialize(),
            };

            var json = commandInfo.Serialize();
            var bytes = Encoding.UTF8.GetBytes(json);
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);

            return await ReadResponseAsync(commandInfo.Id);
        }

        /// <summary>
        /// Sends a command without reading its response. Use when the test cares about a later
        /// server-pushed message rather than the command's own reply, so that reply isn't consumed by an
        /// Id match (which would race the push).
        /// </summary>
        public async Task SendCommandNoWaitAsync(string commandName, object? parameters = null)
        {
            var commandInfo = new SocketCommandInfo(commandName)
            {
                Id = Guid.NewGuid().ToString(),
                Parameters = parameters?.Serialize(),
            };

            var json = commandInfo.Serialize();
            var bytes = Encoding.UTF8.GetBytes(json);
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);
        }

        /// <summary>
        /// Reads until a message whose <c>Name</c> matches arrives (skipping any others, e.g. a different
        /// command's response), for server-pushed commands that carry no request Id to match on. Returns
        /// the push with its typed data so the payload can be asserted.
        /// </summary>
        public async Task<ApiSocketResponse<TResponse>> WaitForCommandAsync<TResponse>(string commandName)
        {
            while (true)
            {
                var message = await ReadMessageAsync();
                if (message == "ping")
                {
                    await SendPongAsync();
                    continue;
                }

                var response = message.Deserialize<ApiSocketResponse<TResponse>>();
                if (response is not null && response.Name == commandName)
                {
                    return response;
                }
            }
        }

        /// <summary>
        /// Reads from the socket until a response with the given ID arrives, without sending anything.
        /// Use this when a command is emitted through a server-side path (e.g. pub/sub) rather than
        /// directly from the client.
        /// </summary>
        public async Task<ApiSocketResponse> WaitForResponseAsync(string? commandId)
        {
            return await ReadResponseAsync(commandId);
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

        private async Task<ApiSocketResponse<TResponse>> ReadResponseAsync<TResponse>(string? expectedId)
        {
            while (true)
            {
                var message = await ReadMessageAsync();
                if (message == "ping")
                {
                    await SendPongAsync();
                    continue;
                }

                var response = message.Deserialize<ApiSocketResponse<TResponse>>();
                if (response is not null && response.Id == expectedId)
                {
                    return response;
                }
            }
        }

        private async Task<ApiSocketResponse> ReadResponseAsync(string? expectedId)
        {
            while (true)
            {
                var message = await ReadMessageAsync();
                if (message == "ping")
                {
                    await SendPongAsync();
                    continue;
                }

                var response = message.Deserialize<ApiSocketResponse>();
                if (response is not null && response.Id == expectedId)
                {
                    return response;
                }
            }
        }

        private async Task<string> ReadMessageAsync()
        {
            var buffer = new byte[4096];
            var result = await _socket.ReceiveAsync(buffer, _cts.Token);
            return Encoding.UTF8.GetString(buffer, 0, result.Count);
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
