using Game.Api.Models.Common;
using Game.Api.Models.Progress;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class StatisticsControllerTests : ApiIntegrationTestBase
    {
        public StatisticsControllerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<HttpClient> SetupAuthenticatedClientAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context, "statsuser", "statspass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            var (authClient, _) = await LoginAndBuildClientAsync("statsuser", "statspass");
            return authClient;
        }

        [Fact]
        public async Task GetStatistics_Authenticated_ReturnsStatistics()
        {
            using var authClient = await SetupAuthenticatedClientAsync();

            var response = await authClient.GetAsync("/api/Statistics", CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<PlayerStatistic>>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
            // For a new player, statistics may be empty
        }

        [Fact]
        public async Task GetStatistics_WithSeededStatistics_ReturnsMappedStatistics()
        {
            int playerId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var user = await TestDataSeeder.CreateUserAsync(context, "statsdata", "statspass");
                var skill = await TestDataSeeder.CreateSkillAsync(context);
                var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
                playerId = player.Id;

                await TestDataSeeder.AddPlayerStatisticAsync(context, playerId, EStatisticType.EnemiesKilled, 7m);
                await TestDataSeeder.AddPlayerStatisticAsync(context, playerId, EStatisticType.EnemiesKilled, 3m, entityId: 1);
            }

            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            var (client, _) = await LoginAndBuildClientAsync("statsdata", "statspass");
            using var authClient = client;

            var response = await authClient.GetAsync("/api/Statistics", CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<PlayerStatistic>>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.Data);

            var stats = result.Data.ToList();
            Assert.Equal(2, stats.Count);

            var total = stats.Single(s => s.StatisticTypeId == EStatisticType.EnemiesKilled && s.EntityId is null);
            Assert.Equal(7m, total.Value);

            var perEnemy = stats.Single(s => s.StatisticTypeId == EStatisticType.EnemiesKilled && s.EntityId == 1);
            Assert.Equal(3m, perEnemy.Value);
        }

        [Fact]
        public async Task GetStatistics_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Statistics", CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
