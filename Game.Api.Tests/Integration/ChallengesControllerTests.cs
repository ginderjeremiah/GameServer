using Game.Api.Models.Common;
using Game.Api.Models.Progress;
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
    public class ChallengesControllerTests : ApiIntegrationTestBase
    {
        public ChallengesControllerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task GetPlayerChallenges_WithSeededProgress_ReturnsMappedChallenges()
        {
            int playerId;
            int challengeId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var user = await TestDataSeeder.CreateUserAsync(context, "challuser", "challpass");
                var skill = await TestDataSeeder.CreateSkillAsync(context);
                var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
                playerId = player.Id;

                var challenge = await TestDataSeeder.CreateChallengeAsync(context);
                challengeId = challenge.Id;
                await TestDataSeeder.AddPlayerChallengeAsync(
                    context, playerId, challengeId, progress: 10m, completed: true, completedAt: DateTime.UtcNow);

            }

            // Reload the caches so the newly seeded challenge is resolvable by id during the read (the
            // caches no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var (client, _) = await LoginAndBuildClientAsync("challuser", "challpass");
            using var authClient = client;

            var response = await authClient.GetAsync("/api/Challenges/Player", CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<PlayerChallenge>>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.Data);

            var playerChallenge = Assert.Single(result.Data);
            Assert.Equal(challengeId, playerChallenge.ChallengeId);
            Assert.Equal(10m, playerChallenge.Progress);
            Assert.True(playerChallenge.Completed);
            Assert.NotNull(playerChallenge.CompletedAt);
        }

        [Fact]
        public async Task GetPlayerChallenges_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Challenges/Player", CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
