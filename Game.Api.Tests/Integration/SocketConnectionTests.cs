using Game.Api.Models.Common;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class SocketConnectionTests : ApiIntegrationTestBase
    {
        public SocketConnectionTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task Connect_Authenticated_Succeeds()
        {
            var (userId, _) = await SeedAndLoginAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            Assert.Equal(WebSocketState.Open, socketClient.State);
        }

        [Fact]
        public async Task Connect_Unauthenticated_Fails()
        {
            // A genuinely unauthenticated handshake — no token offered at all — is rejected. (A valid token
            // with no cached session is a different, authenticated case; see the rehydration test below.)
            var wsClient = Factory.Server.CreateWebSocketClient();

            // The TestServer client throws InvalidOperationException with the rejected status code baked
            // into the message when the handshake doesn't complete with a 101 — tightened from ThrowsAny so
            // this can't pass on, say, a 500 as readily as the intended 401-rejected upgrade.
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => wsClient.ConnectAsync(new Uri("ws://localhost/socket"), CancellationToken));
            Assert.Contains("401", ex.Message);
        }

        [Fact]
        public async Task Socket_Unauthenticated_ReturnsErrorEnvelope()
        {
            // A plain HTTP request to /socket with no token short-circuits to 401 before any upgrade, so the
            // body carries the project's { errorMessage } envelope rather than being empty.
            var response = await Client.GetAsync("/socket", CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(body);
            Assert.False(string.IsNullOrWhiteSpace(body.ErrorMessage));
        }

        [Fact]
        public async Task Socket_AuthenticatedButNotWebSocketUpgrade_ReturnsErrorEnvelope()
        {
            // An authenticated but non-upgrade request to /socket short-circuits to 400 before any upgrade, so
            // the body carries the { errorMessage } envelope rather than being empty.
            var (userId, playerId) = await SeedAsync();
            var authedClient = await CreateAuthenticatedClient(userId, playerId);

            var response = await authedClient.GetAsync("/socket", CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(body);
            Assert.False(string.IsNullOrWhiteSpace(body.ErrorMessage));
        }

        [Fact]
        public async Task Socket_AuthenticatedButPlayerNotLoadable_ReturnsErrorEnvelope()
        {
            // The player-load short-circuit sits after the WebSocket-upgrade check, so — unlike the 401/400
            // cases — it can't be reached with a plain GET (that stops at the 400 branch). The request is
            // driven through the pipeline presenting as a WebSocket upgrade (a stubbed feature), but the
            // socket is never accepted: a post-selection token naming a player that can't be loaded
            // (archived/deleted between requests, mirrored here by a token minted for a never-persisted id)
            // is a genuine missing-resource failure, distinct from the pre-selection case below — it
            // short-circuits to 404 with the { errorMessage } envelope still written to the (un-upgraded) body.
            var (userId, _) = await SeedAsync();
            var token = TestAuthHelper.CreateAccessToken(userId, playerId: 999_999_999);

            var context = await Factory.Server.SendAsync(ctx =>
            {
                ctx.Request.Method = HttpMethods.Get;
                ctx.Request.Path = "/socket";
                ctx.Request.Headers["Sec-WebSocket-Protocol"] = token;
                ctx.Features.Set<IHttpWebSocketFeature>(new StubWebSocketFeature());
            }, CancellationToken);

            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
            // The response body stream isn't seekable, so deserialize straight from its start.
            var body = await JsonSerializer.DeserializeAsync<ApiResponse>(
                context.Response.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                CancellationToken);
            Assert.NotNull(body);
            Assert.False(string.IsNullOrWhiteSpace(body.ErrorMessage));
        }

        [Fact]
        public async Task Socket_AuthenticatedNoPlayerSelected_ReturnsNoPlayerSelectedNotOpaque404()
        {
            // A pre-selection token (post-Login, pre-SelectPlayer) is a normal, documented flow state
            // (docs/backend-auth.md) — a well-behaved client never opens a socket in this state, but the
            // handshake must still reject it as its own distinguishable category (mirroring
            // AuthController.Status), not the generic 404 a genuinely unloadable player gets.
            var (userId, _) = await SeedAsync();
            var token = TestAuthHelper.CreateAccessToken(userId);

            var context = await Factory.Server.SendAsync(ctx =>
            {
                ctx.Request.Method = HttpMethods.Get;
                ctx.Request.Path = "/socket";
                ctx.Request.Headers["Sec-WebSocket-Protocol"] = token;
                ctx.Features.Set<IHttpWebSocketFeature>(new StubWebSocketFeature());
            }, CancellationToken);

            Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
            var body = await JsonSerializer.DeserializeAsync<ApiResponse>(
                context.Response.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                CancellationToken);
            Assert.NotNull(body);
            Assert.False(string.IsNullOrWhiteSpace(body.ErrorMessage));
        }

        [Fact]
        public async Task Connect_ValidTokenWithNoSessionCache_RehydratesAndSucceeds()
        {
            // A valid token whose session was never established in Redis (or was evicted) must connect by
            // rehydrating the session from the user's player binding, not be rejected as unauthenticated (#693).
            var (userId, _) = await SeedAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            Assert.Equal(WebSocketState.Open, socketClient.State);
        }

        [Fact]
        public async Task Connect_SetsPresenceKeyWithTtl()
        {
            var (userId, playerId) = await SeedAndLoginAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            // Round-trip a command so registration (which sets the presence key + TTL) has completed.
            await socketClient.SendCommandAsync<object>("GetStatisticTypes");

            var ttl = await GetPresenceTtlAsync(playerId);

            // The presence key carries a TTL, so a connection that vanishes without a clean close can't
            // leave the key lingering forever.
            Assert.NotNull(ttl);
            Assert.True(ttl > TimeSpan.Zero, $"Expected a positive TTL but got {ttl}.");
            Assert.True(ttl <= TimeSpan.FromSeconds(30), $"Expected TTL <= 30s but got {ttl}.");
        }

        [Fact]
        public async Task SocketActivity_RefreshesPresenceTtl()
        {
            var (userId, playerId) = await SeedAndLoginAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            await socketClient.SendCommandAsync<object>("GetStatisticTypes");

            var initial = await GetPresenceTtlAsync(playerId);

            // Let the TTL decay (the test client sends no heartbeat of its own), then prove a fresh
            // inbound message bumps it back up.
            await Task.Delay(2000, CancellationToken);
            var decayed = await GetPresenceTtlAsync(playerId);

            await socketClient.SendCommandAsync<object>("GetStatisticTypes");
            var refreshed = await GetPresenceTtlAsync(playerId);

            Assert.NotNull(initial);
            Assert.NotNull(decayed);
            Assert.NotNull(refreshed);
            Assert.True(decayed < initial, $"Expected the TTL to decay below {initial} but it was {decayed}.");
            Assert.True(refreshed > decayed, $"Expected activity to refresh the TTL above {decayed} but it was {refreshed}.");
        }

        /// <summary>
        /// Presents a request to the pipeline as a WebSocket upgrade so the interceptor reaches its
        /// post-upgrade checks, without ever completing a handshake. <see cref="AcceptAsync"/> is never
        /// invoked by tests that short-circuit before the upgrade, so it deliberately throws.
        /// </summary>
        private sealed class StubWebSocketFeature : IHttpWebSocketFeature
        {
            public bool IsWebSocketRequest => true;

            public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context) =>
                throw new InvalidOperationException("The stub WebSocket feature never accepts a connection.");
        }
    }
}
