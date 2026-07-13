using Game.Abstractions.Contracts;
using Game.Api.Models.Common;
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
    /// <summary>
    /// Integration tests for the Tags reference reads — the only remaining HTTP-exclusive reference
    /// endpoints (consumed by the admin Workbench). They are read live from the database (no in-memory
    /// cache), so seeding a tag and reading it back exercises the controller and the IAsyncEnumerable
    /// projection. The endpoints are admin-only (#690), so the authenticated reader is granted the Admin role.
    /// </summary>
    [Collection("Integration")]
    public class TagsControllerTests : ApiIntegrationTestBase
    {
        private const string Username = "tagsuser";
        private const string Password = "tagspass";

        public TagsControllerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<HttpClient> SeedTagAndAuthenticateAsync(string tagName)
        {
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var user = await TestDataSeeder.CreateUserAsync(context, Username, Password);
                await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                await TestDataSeeder.AssignRoleToUserAsync(context, user.Id, ERole.Admin);
                await TestDataSeeder.CreateTagAsync(context, tagName, ETagCategory.Accessory);
            }

            // CreatePlayerAsync seeds the player's class directly; reload the reference caches so SelectPlayer
            // can resolve it when projecting the player's locked-base fingerprint (#1223).
            await ReloadReferenceCachesAsync();

            var (client, _) = await LoginAndBuildClientAsync(Username, Password);
            return client;
        }

        [Fact]
        public async Task Tags_ReturnsSeededTag()
        {
            using var client = await SeedTagAndAuthenticateAsync("RefTag");

            var response = await client.GetAsync("/api/Tags", CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<Tag>>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.Data);
            var tag = Assert.Single(result.Data, t => t.Name == "RefTag");
            Assert.Equal((int)ETagCategory.Accessory, tag.TagCategoryId);
        }

        [Fact]
        public async Task TagCategories_ReturnsIntrinsicCategories()
        {
            using var client = await SeedTagAndAuthenticateAsync("RefTagForCategories");

            var response = await client.GetAsync("/api/Tags/TagCategories", CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<TagCategory>>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.Data);
            // Categories are intrinsic, migration-seeded reference data, so every ETagCategory is present.
            var categories = result.Data.ToList();
            Assert.Contains(categories, c => c.Id == (int)ETagCategory.Accessory);
            Assert.Equal(Enum.GetValues<ETagCategory>().Length, categories.Count);
        }

        [Fact]
        public async Task Tags_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Tags", CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData("/api/Tags")]
        [InlineData("/api/Tags/TagCategories")]
        public async Task TagsEndpoints_NonAdmin_Returns403(string url)
        {
            int userId, playerId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var user = await TestDataSeeder.CreateUserAsync(context, "nonadmintags", "nonadminpass");
                var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                userId = user.Id;
                playerId = player.Id;
            }

            // Non-admin token: the endpoints are gated by AdminRoleAuthorizationFilter (#690).
            using var client = await CreateAuthenticatedClient(userId, playerId);

            var response = await client.GetAsync(url, CancellationToken);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}
