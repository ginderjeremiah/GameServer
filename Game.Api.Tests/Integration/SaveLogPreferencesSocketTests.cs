using Game.Api.Models.Player;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class SaveLogPreferencesSocketTests : ApiIntegrationTestBase
    {
        private const string Username = "logprefuser";
        private const string Password = "logprefpass";

        public SaveLogPreferencesSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<(int UserId, int PlayerId)> SeedPlayerAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context, Username, Password);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            return (user.Id, player.Id);
        }

        [Fact]
        public async Task SaveLogPreferences_PersistsChangesToCachedPlayer()
        {
            var (userId, playerId) = await SeedPlayerAsync();
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandRawAsync("SaveLogPreferences", new[]
            {
                new { Id = (int)ELogType.Damage, Enabled = false },
                new { Id = (int)ELogType.Debug, Enabled = true },
            });

            Assert.Null(response.Error);

            var prefs = await WaitForLogPreferenceAsync(playerId, ELogType.Damage, enabled: false);
            Assert.False(prefs.Single(p => p.Id == ELogType.Damage).Enabled);
            Assert.True(prefs.Single(p => p.Id == ELogType.Debug).Enabled);
        }

        [Fact]
        public async Task SaveLogPreferences_UnknownLogType_ReturnsError()
        {
            var (userId, _) = await SeedPlayerAsync();
            // Logging in creates the session the WebSocket handshake requires.
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var response = await socketClient.SendCommandRawAsync("SaveLogPreferences", new[]
            {
                new { Id = 9999, Enabled = false },
            });

            Assert.NotNull(response.Error);
        }

        /// <summary>
        /// The save writes the cached player fire-and-forget, so poll the player snapshot
        /// until the expected preference lands (or fail after a short budget).
        /// </summary>
        private async Task<List<LogPreference>> WaitForLogPreferenceAsync(int playerId, ELogType type, bool enabled)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var prefs = (await GetPersistedPlayerAsync(playerId)).LogPreferences;
                if (prefs.Any(p => p.Id == type && p.Enabled == enabled))
                {
                    return prefs;
                }

                await Task.Delay(50, CancellationToken);
            }

            Assert.Fail($"Log preference {type} did not reach expected value {enabled}.");
            return [];
        }
    }
}
