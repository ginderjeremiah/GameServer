using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class SocketConnectionTests : ApiIntegrationTestBase
    {
        public SocketConnectionTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        /// <summary>
        /// Seeds test data, logs in via HTTP, and returns the userId for WebSocket auth.
        /// </summary>
        private async Task<int> SeedAndLoginAsync(string username = "socketuser", string password = "socketpass")
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, username, password);
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            // Login to create session in Redis
            var loginResponse = await Client.PostAsJsonAsync("/api/Login",
                new { Username = username, Password = password });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            return user.Id;
        }

        [Fact]
        public async Task Connect_Authenticated_Succeeds()
        {
            var userId = await SeedAndLoginAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            Assert.Equal(WebSocketState.Open, socketClient.State);
        }

        [Fact]
        public async Task Connect_Unauthenticated_Fails()
        {
            // Don't seed any user or login — use a bogus userId
            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();

            // With no session in Redis, the middleware should reject the connection
            await Assert.ThrowsAnyAsync<Exception>(
                () => socketClient.ConnectAsync(wsClient, 99999));
        }
    }
}
