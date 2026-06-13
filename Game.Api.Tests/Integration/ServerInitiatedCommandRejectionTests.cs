using Game.Api.Sockets.Commands;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Verifies the inbound client path rejects server-initiated-only commands
    /// (<see cref="IServerInitiatedCommand"/>) rather than executing them — the hardening from #411.
    /// The backplane-delivered path for these commands stays covered by
    /// <see cref="ChallengeCompletedSocketTests"/> and <see cref="SocketManagerServiceTests"/>.
    /// </summary>
    [Collection("Integration")]
    public class ServerInitiatedCommandRejectionTests : ApiIntegrationTestBase
    {
        private const string Username = "serverinituser";
        private const string Password = "serverinitpass";

        public ServerInitiatedCommandRejectionTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        private async Task<int> SeedAndLoginAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, Username, Password);
            await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await LoginAsync(Username, Password);
            return user.Id;
        }

        [Fact]
        public async Task ClientInvoking_ChallengeCompleted_IsRejectedWithError()
        {
            var userId = await SeedAndLoginAsync();

            await using var socketClient = new TestSocketClient();
            await socketClient.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId);

            var response = await socketClient.SendCommandRawAsync(
                nameof(ChallengeCompleted),
                new { ChallengeId = 0, RewardItemId = (int?)null, RewardItemModId = (int?)null, RewardSkillId = (int?)null });

            Assert.Equal("Command cannot be invoked by the client.", response.Error);
        }

        [Fact]
        public async Task ClientInvoking_SocketReplaced_IsRejectedAndSocketStaysOpen()
        {
            var userId = await SeedAndLoginAsync();

            await using var socketClient = new TestSocketClient();
            await socketClient.ConnectAsync(Factory.Server.CreateWebSocketClient(), userId);

            // SocketReplaced would close the socket if it executed; rejection must leave it open.
            var response = await socketClient.SendCommandRawAsync(nameof(SocketReplaced));

            Assert.Equal("Command cannot be invoked by the client.", response.Error);
            Assert.Equal(WebSocketState.Open, socketClient.State);
        }
    }
}
