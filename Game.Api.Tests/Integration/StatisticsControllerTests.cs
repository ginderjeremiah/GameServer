using Game.Api.Models.Common;
using Game.Api.Models.Statistics;
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

            var loginCreds = new { Username = "statsuser", Password = "statspass" };
            var loginResponse = await Client.PostAsJsonAsync("/api/Login", loginCreds);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            var authClient = Factory.CreateClient();
            var cookies = loginResponse.Headers.GetValues("Set-Cookie");
            foreach (var cookie in cookies)
            {
                authClient.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
            }

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
        public async Task GetStatistics_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Statistics", CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
