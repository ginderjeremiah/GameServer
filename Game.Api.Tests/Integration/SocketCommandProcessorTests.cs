using Game.Api.Services;
using Game.Api.Sockets.Commands;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class SocketCommandProcessorTests : ApiIntegrationTestBase
    {
        private const string Username = "cmdprocuser";
        private const string Password = "cmdprocpass";

        public SocketCommandProcessorTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        private async Task<(int UserId, int PlayerId)> SeedAndLoginAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, Username, Password);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await LoginAsync(Username, Password);
            return (user.Id, player.Id);
        }

        [Fact]
        public async Task PubSub_UnknownCommandFollowedByValidCommand_ValidCommandStillExecutes()
        {
            var (userId, playerId) = await SeedAndLoginAsync();

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            // Confirm the socket's pub/sub subscription is registered before emitting via the server path.
            await socketClient.SendCommandAsync<object>("GetStatisticTypes");

            using var scope = CreateScope();
            var socketManager = scope.ServiceProvider.GetRequiredService<SocketManagerService>();

            var validCommandId = Guid.NewGuid().ToString();

            // An unknown command deterministically throws in CreateCommand / the command loop. If the
            // processor hot-loops on failure it never processes the valid command below.
            await socketManager.EmitSocketCommand(new SocketCommandInfo("NonExistentCommand"), playerId);
            await socketManager.EmitSocketCommand(new SocketCommandInfo("GetStatisticTypes") { Id = validCommandId }, playerId);

            var response = await socketClient.WaitForResponseAsync(validCommandId);
            Assert.Null(response.Error);
        }
    }
}
