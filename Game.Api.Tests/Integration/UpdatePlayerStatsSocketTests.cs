using Game.Api.Models.Attributes;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class UpdatePlayerStatsSocketTests : ApiIntegrationTestBase
    {
        private const string Username = "attrsocketuser";
        private const string Password = "attrsocketpass";

        public UpdatePlayerStatsSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        /// <summary>
        /// Seeds a user + player with the given available stat points. The seeded player starts with
        /// Strength/Endurance at 50 each (StatPointsUsed = 100), so the available pool is
        /// <paramref name="statPointsGained"/> − 100.
        /// </summary>
        private async Task<(int UserId, int PlayerId)> SeedPlayerAsync(int statPointsGained)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context, Username, Password);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            player.StatPointsGained = statPointsGained;
            await context.SaveChangesAsync();
            // The caches no longer lazily refill, so reload them to resolve the player on load.
            await ReloadReferenceCachesAsync();

            return (user.Id, player.Id);
        }

        [Fact]
        public async Task UpdatePlayerStats_ValidUpdate_ReturnsAndPersistsNewAllocation()
        {
            // 6 available points (106 gained − 100 used).
            var (userId, playerId) = await SeedPlayerAsync(statPointsGained: 106);
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var updates = new[] { new { attributeId = (int)EAttribute.Strength, amount = 3 } };
            var response = await socketClient.SendCommandAsync<UpdatePlayerStatsResponse>("UpdatePlayerStats", updates);

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Equal(53m, response.Data.Attributes.Single(a => a.AttributeId == EAttribute.Strength).Amount);
            // The response carries the authoritative post-spend total so the client adopts it absolutely.
            Assert.Equal(103, response.Data.StatPointsUsed);

            // The save writes the cached player fire-and-forget, so poll the player snapshot until the
            // new allocation lands (this is the persistence the bug report said could silently revert).
            var persisted = await WaitForStrengthAsync(playerId, expected: 53m);
            Assert.Equal(53m, persisted);
        }

        [Fact]
        public async Task UpdatePlayerStats_SpendMoreThanAvailable_ReturnsError()
        {
            // Default seeded player has 0 available points (100 gained − 100 used).
            var (userId, _) = await SeedPlayerAsync(statPointsGained: 100);
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var updates = new[] { new { attributeId = (int)EAttribute.Strength, amount = 999 } };
            var response = await socketClient.SendCommandAsync<UpdatePlayerStatsResponse>("UpdatePlayerStats", updates);

            Assert.NotNull(response.Error);
            // The rejection still returns the unchanged authoritative state for the client to reconcile onto.
            Assert.NotNull(response.Data);
            Assert.Equal(50m, response.Data.Attributes.Single(a => a.AttributeId == EAttribute.Strength).Amount);
            Assert.Equal(100, response.Data.StatPointsUsed);
        }

        [Fact]
        public async Task UpdatePlayerStats_UnknownAttribute_ReturnsErrorAndLeavesAllocationsUnchanged()
        {
            // 6 available points (106 gained − 100 used). The seeded player only has rows for Strength and
            // Endurance, so Luck has no allocation row — allocating into it must be rejected rather than
            // silently reporting success, and the valid Strength delta in the same set must not apply (#488).
            var (userId, _) = await SeedPlayerAsync(statPointsGained: 106);
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            var updates = new[]
            {
                new { attributeId = (int)EAttribute.Strength, amount = 3 },
                new { attributeId = (int)EAttribute.Luck, amount = 2 },
            };
            var response = await socketClient.SendCommandAsync<UpdatePlayerStatsResponse>("UpdatePlayerStats", updates);

            Assert.NotNull(response.Error);
            Assert.NotNull(response.Data);
            Assert.Equal(50m, response.Data.Attributes.Single(a => a.AttributeId == EAttribute.Strength).Amount);
            Assert.Equal(100, response.Data.StatPointsUsed);
        }

        /// <summary>
        /// Polls the persisted player snapshot until the player's Strength allocation reaches the expected
        /// value (the cache write is fire-and-forget), failing after a short budget.
        /// </summary>
        private async Task<decimal> WaitForStrengthAsync(int playerId, decimal expected)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var data = await GetPersistedPlayerAsync(playerId);
                var strength = data.Attributes.Single(a => a.AttributeId == EAttribute.Strength).Amount;
                if (strength == expected)
                {
                    return strength;
                }

                await Task.Delay(50, CancellationToken);
            }

            Assert.Fail("Player Strength allocation did not reach the expected value.");
            return 0m;
        }
    }
}
