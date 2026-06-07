using Game.Api.Models.Enemies;
using Game.Api.Models.Items;
using Game.Api.Models.Progress;
using Game.Api.Models.ReferenceData;
using Game.Api.Models.Skills;
using Game.Api.Models.Zones;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using Xunit;
using Attribute = Game.Api.Models.Attributes.Attribute;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Integration tests for the reference-data socket commands (see issue #34),
    /// which mirror the read-only reference-data HTTP endpoints over the socket.
    /// </summary>
    [Collection("Integration")]
    public class ReferenceDataSocketTests : ApiIntegrationTestBase
    {
        public ReferenceDataSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        /// <summary>
        /// Seeds a connectable player plus a slice of each kind of static reference
        /// data, logs in, and returns the userId for the WebSocket handshake.
        /// </summary>
        private async Task<int> SeedReferenceDataAndLoginAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context, "RefFireball");
            var enemy = await TestDataSeeder.CreateEnemyAsync(context, "RefGoblin");
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            await TestDataSeeder.CreateZoneAsync(context, "RefZone");
            await TestDataSeeder.CreateItemAsync(context, "RefSword");
            await TestDataSeeder.CreateItemModAsync(context, "RefMod");

            var user = await TestDataSeeder.CreateUserAsync(context, "refdatauser", "refdatapass");
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            // Login to create the session in Redis that the socket handshake requires.
            var loginResponse = await Client.PostAsJsonAsync("/api/Login",
                new { Username = "refdatauser", Password = "refdatapass" });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            return user.Id;
        }

        private async Task<TestSocketClient> ConnectAsync(int userId)
        {
            var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            Assert.Equal(WebSocketState.Open, socketClient.State);
            return socketClient;
        }

        [Fact]
        public async Task GetEnemies_ReturnsSeededEnemies()
        {
            var userId = await SeedReferenceDataAndLoginAsync();
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<Enemy>>("GetEnemies");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Contains(response.Data, e => e.Name == "RefGoblin");
        }

        [Fact]
        public async Task GetZones_ReturnsSeededZones()
        {
            var userId = await SeedReferenceDataAndLoginAsync();
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<Zone>>("GetZones");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Contains(response.Data, z => z.Name == "RefZone");
        }

        [Fact]
        public async Task GetSkills_ReturnsSeededSkills()
        {
            var userId = await SeedReferenceDataAndLoginAsync();
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<Skill>>("GetSkills");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Contains(response.Data, s => s.Name == "RefFireball");
        }

        [Fact]
        public async Task GetItems_ReturnsSeededItems()
        {
            var userId = await SeedReferenceDataAndLoginAsync();
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<Item>>("GetItems");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Contains(response.Data, i => i.Name == "RefSword");
        }

        [Fact]
        public async Task GetItemMods_ReturnsSeededItemMods()
        {
            var userId = await SeedReferenceDataAndLoginAsync();
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<ItemMod>>("GetItemMods");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Contains(response.Data, m => m.Name == "RefMod");
        }

        [Fact]
        public async Task GetAttributes_ReturnsIntrinsicAttributes()
        {
            var userId = await SeedReferenceDataAndLoginAsync();
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<Attribute>>("GetAttributes");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.NotEmpty(response.Data);
        }

        [Fact]
        public async Task GetChallengeTypes_ReturnsIntrinsicChallengeTypes()
        {
            var userId = await SeedReferenceDataAndLoginAsync();
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<ChallengeType>>("GetChallengeTypes");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.NotEmpty(response.Data);
        }

        [Fact]
        public async Task GetChallenges_ReturnsWithoutError()
        {
            var userId = await SeedReferenceDataAndLoginAsync();
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<Challenge>>("GetChallenges");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
        }

        [Fact]
        public async Task GetStatisticTypes_ReturnsIntrinsicStatisticTypes()
        {
            var userId = await SeedReferenceDataAndLoginAsync();
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<StatisticType>>("GetStatisticTypes");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.NotEmpty(response.Data);
        }

        [Fact]
        public async Task GetReferenceDataVersions_ReturnsANonEmptyVersionForEveryReferenceDataSet()
        {
            var userId = await SeedReferenceDataAndLoginAsync();
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<ReferenceDataVersion>>("GetReferenceDataVersions");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            // One entry per Get* reference-data command the loading screen pulls.
            Assert.Equal(
                ["GetAttributes", "GetChallengeTypes", "GetChallenges", "GetEnemies", "GetItemMods",
                 "GetItems", "GetSkills", "GetStatisticTypes", "GetZones"],
                response.Data.Select(v => v.Command).OrderBy(c => c, StringComparer.Ordinal));
            Assert.All(response.Data, v => Assert.False(string.IsNullOrEmpty(v.Version)));
        }

        [Fact]
        public async Task GetReferenceDataVersions_ReturnsStableVersionsForUnchangedData()
        {
            var userId = await SeedReferenceDataAndLoginAsync();
            await using var socketClient = await ConnectAsync(userId);

            var first = await socketClient.SendCommandAsync<List<ReferenceDataVersion>>("GetReferenceDataVersions");
            var second = await socketClient.SendCommandAsync<List<ReferenceDataVersion>>("GetReferenceDataVersions");

            Assert.Null(first.Error);
            Assert.Null(second.Error);
            Assert.Equal(
                first.Data.Select(v => (v.Command, v.Version)),
                second.Data.Select(v => (v.Command, v.Version)));
        }
    }
}
