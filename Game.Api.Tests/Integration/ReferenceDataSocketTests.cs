using Game.Abstractions.Contracts;
using Game.Api.Models.Progress;
using Game.Api.Models.ReferenceData;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using Xunit;
using Attribute = Game.Api.Models.Attributes.Attribute;
using Lesson = Game.Abstractions.Contracts.Lesson;
using Path = Game.Abstractions.Contracts.Path;

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
            await TestDataSeeder.CreateProficiencyAsync(context, "RefBlades");
            await TestDataSeeder.CreateClassAsync(context, "RefWarrior");

            var user = await TestDataSeeder.CreateUserAsync(context, "refdatauser", "refdatapass");
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            // Reload the caches so the Get* socket commands serve the seeded reference data (the caches no
            // longer lazily refill).
            await ReloadReferenceCachesAsync();

            // Login to create the session in Redis that the socket handshake requires.
            var loginResponse = await Client.PostAsJsonAsync("/api/Login",
                new { Username = "refdatauser", Password = "refdatapass" });
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            return user.Id;
        }

        private async Task<TestSocketClient> ConnectAsync(int userId, params string[] roles)
        {
            var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId, playerId: null, roles);
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
        public async Task GetSkills_RedactsDesignerNotesForNonAdminConnection()
        {
            int userId, skillId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                skillId = (await TestDataSeeder.CreateSkillAsync(context, "RefNotedSkill", designerNotes: "Why this skill exists.")).Id;

                var user = await TestDataSeeder.CreateUserAsync(context, "notesplayeruser", "notespass");
                await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                userId = user.Id;
            }

            await ReloadReferenceCachesAsync();
            await LoginAsync("notesplayeruser", "notespass");

            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<Skill>>("GetSkills");

            Assert.Null(response.Error);
            var skill = Assert.Single(response.Data, s => s.Id == skillId);
            Assert.Equal("", skill.DesignerNotes);
        }

        [Fact]
        public async Task GetSkills_IncludesDesignerNotesForAdminConnection()
        {
            int userId, skillId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                skillId = (await TestDataSeeder.CreateSkillAsync(context, "RefNotedSkillAdmin", designerNotes: "Why this skill exists.")).Id;

                var user = await TestDataSeeder.CreateUserAsync(context, "notesadminuser", "notespass");
                await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                userId = user.Id;
            }

            await ReloadReferenceCachesAsync();
            await LoginAsync("notesadminuser", "notespass");

            // The admin role is minted straight onto the socket handshake token (mirroring how the
            // Workbench's own connection carries it); it doesn't require a DB-persisted role grant.
            await using var socketClient = await ConnectAsync(userId, nameof(ERole.Admin));

            var response = await socketClient.SendCommandAsync<List<Skill>>("GetSkills");

            Assert.Null(response.Error);
            var skill = Assert.Single(response.Data, s => s.Id == skillId);
            Assert.Equal("Why this skill exists.", skill.DesignerNotes);
        }

        [Fact]
        public async Task GetReferenceDataVersions_SkillsVersionMatchesForAdminAndNonAdminConnections()
        {
            int playerUserId, adminUserId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.CreateSkillAsync(context, "RefVersionParitySkill", designerNotes: "Internal rationale.");

                var player = await TestDataSeeder.CreateUserAsync(context, "versionparityplayer", "versionparitypass");
                await TestDataSeeder.CreatePlayerAsync(context, player.Id);
                playerUserId = player.Id;

                var admin = await TestDataSeeder.CreateUserAsync(context, "versionparityadmin", "versionparitypass");
                await TestDataSeeder.CreatePlayerAsync(context, admin.Id);
                adminUserId = admin.Id;
            }

            await ReloadReferenceCachesAsync();
            await LoginAsync("versionparityplayer", "versionparitypass");
            await LoginAsync("versionparityadmin", "versionparitypass");

            await using var playerSocket = await ConnectAsync(playerUserId);
            await using var adminSocket = await ConnectAsync(adminUserId, nameof(ERole.Admin));

            var playerVersions = await playerSocket.SendCommandAsync<List<ReferenceDataVersion>>("GetReferenceDataVersions");
            var adminVersions = await adminSocket.SendCommandAsync<List<ReferenceDataVersion>>("GetReferenceDataVersions");

            var playerSkillsVersion = Assert.Single(playerVersions.Data, v => v.Command == "GetSkills").Version;
            var adminSkillsVersion = Assert.Single(adminVersions.Data, v => v.Command == "GetSkills").Version;

            // The version hash is computed from the full (unredacted) contract regardless of who's asking, so
            // authoring a note bumps it once for every client — only the wire payload differs by role.
            Assert.Equal(adminSkillsVersion, playerSkillsVersion);
        }

        [Fact]
        public async Task GetSkillRecipes_ReturnsSeededSkillRecipesWithInputsAndConditions()
        {
            int userId, recipeId, resultSkillId, inputSkillId, proficiencyId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                resultSkillId = (await TestDataSeeder.CreateSkillAsync(context, "RefSynthResult",
                    acquisition: ESkillAcquisition.Synthesis)).Id;
                inputSkillId = (await TestDataSeeder.CreateSkillAsync(context, "RefSynthInput")).Id;
                proficiencyId = (await TestDataSeeder.CreateProficiencyAsync(context, "RefSynthGate")).Id;
                recipeId = (await TestDataSeeder.CreateSkillRecipeAsync(context, resultSkillId, [inputSkillId],
                    [(proficiencyId, 3)])).Id;

                var user = await TestDataSeeder.CreateUserAsync(context, "recipesocketuser", "recipesocketpass");
                await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                userId = user.Id;
            }

            await ReloadReferenceCachesAsync();
            await LoginAsync("recipesocketuser", "recipesocketpass");

            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<SkillRecipe>>("GetSkillRecipes");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            var recipe = Assert.Single(response.Data, r => r.Id == recipeId);
            Assert.Equal(resultSkillId, recipe.ResultSkillId);
            Assert.Equal([inputSkillId], recipe.InputSkillIds);
            var condition = Assert.Single(recipe.Conditions);
            Assert.Equal(proficiencyId, condition.ProficiencyId);
            Assert.Equal(3, condition.MinLevel);
            Assert.Null(recipe.RetiredAt);
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
        public async Task GetAttributes_SerializesAttributeDisplayMetadata()
        {
            var userId = await SeedReferenceDataAndLoginAsync();
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<Attribute>>("GetAttributes");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.NotEmpty(response.Data);

            // The enriched display metadata round-trips through the API model and socket serialization.
            var strength = Assert.Single(response.Data, a => a.Id == EAttribute.Strength);
            Assert.Equal(EAttributeType.Primary, strength.AttributeType);
            Assert.Equal("STR", strength.Code);
            Assert.False(strength.IsPercentage);
            Assert.False(strength.IsHarmful);
            Assert.Equal(0, strength.Decimals);

            var cooldownRecovery = Assert.Single(response.Data, a => a.Id == EAttribute.CooldownRecovery);
            Assert.True(cooldownRecovery.IsPercentage);
            Assert.Equal(0, cooldownRecovery.Decimals);

            var bleedDot = Assert.Single(response.Data, a => a.Id == EAttribute.BleedDamagePerSecond);
            Assert.Equal(EAttributeType.Status, bleedDot.AttributeType);
            Assert.True(bleedDot.IsHarmful);

            // The poison/burn accumulators append after the amp/resist block (#1320 Area C).
            var burnDot = Assert.Single(response.Data, a => a.Id == EAttribute.BurnDamagePerSecond);
            Assert.Equal(EAttributeType.Status, burnDot.AttributeType);
            Assert.True(burnDot.IsHarmful);
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
        public async Task GetChallenges_PopulatedChallenge_MapsDomainToContract()
        {
            int userId;
            int challengeId;
            int enemyId;
            int itemId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();

                var enemy = await TestDataSeeder.CreateEnemyAsync(context, "ChallengeTarget");
                var item = await TestDataSeeder.CreateItemAsync(context, "ChallengeReward");
                var challenge = await TestDataSeeder.CreateChallengeAsync(context,
                    name: "Slay 10 Goblins",
                    challengeTypeId: EChallengeType.EnemiesKilled,
                    progressGoal: 10m,
                    targetEntityId: enemy.Id,
                    rewardItemId: item.Id);
                challengeId = challenge.Id;
                enemyId = enemy.Id;
                itemId = item.Id;

                var user = await TestDataSeeder.CreateUserAsync(context, "challsocketuser", "challsocketpass");
                await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                userId = user.Id;
            }

            // Reload so the seeded challenge is resolvable, then log in to create the socket session.
            await ReloadReferenceCachesAsync();
            await LoginAsync("challsocketuser", "challsocketpass");

            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<Challenge>>("GetChallenges");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);

            // The ToContract mapping flattens the domain ChallengeType to its id plus the derived
            // statistic/entity dimensions — exercised here by a fully-populated challenge.
            var mapped = Assert.Single(response.Data, c => c.Id == challengeId);
            Assert.Equal("Slay 10 Goblins", mapped.Name);
            Assert.Equal(EChallengeType.EnemiesKilled, mapped.ChallengeTypeId);
            Assert.Equal(EStatisticType.EnemiesKilled, mapped.StatisticType);
            Assert.Equal(EEntityType.Enemy, mapped.EntityType);
            Assert.Equal(enemyId, mapped.TargetEntityId);
            Assert.Equal(10m, mapped.ProgressGoal);
            Assert.Equal(itemId, mapped.RewardItemId);
            Assert.Null(mapped.RetiredAt);
        }

        [Fact]
        public async Task GetClasses_ReturnsSeededClasses()
        {
            var userId = await SeedReferenceDataAndLoginAsync();
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<Class>>("GetClasses");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Contains(response.Data, c => c.Name == "RefWarrior");
        }

        [Fact]
        public async Task GetProficiencies_ReturnsSeededProficiencies()
        {
            var userId = await SeedReferenceDataAndLoginAsync();
            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<Proficiency>>("GetProficiencies");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Contains(response.Data, p => p.Name == "RefBlades");
        }

        [Fact]
        public async Task GetPaths_ReturnsSeededPaths()
        {
            int userId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var path = await TestDataSeeder.CreatePathAsync(context, "RefPyromancy");
                await TestDataSeeder.CreateProficiencyAsync(context, "RefFlameTier", pathId: path.Id);

                var user = await TestDataSeeder.CreateUserAsync(context, "pathsocketuser", "pathsocketpass");
                await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                userId = user.Id;
            }

            await ReloadReferenceCachesAsync();
            await LoginAsync("pathsocketuser", "pathsocketpass");

            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<Path>>("GetPaths");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Contains(response.Data, p => p.Name == "RefPyromancy");
        }

        [Fact]
        public async Task GetLessons_ReturnsSeededLessons()
        {
            int userId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.CreateLessonAsync(context, key: "ref-lesson", name: "RefLesson");

                var user = await TestDataSeeder.CreateUserAsync(context, "lessonsocketuser", "lessonsocketpass");
                await TestDataSeeder.CreatePlayerAsync(context, user.Id);
                userId = user.Id;
            }

            await ReloadReferenceCachesAsync();
            await LoginAsync("lessonsocketuser", "lessonsocketpass");

            await using var socketClient = await ConnectAsync(userId);

            var response = await socketClient.SendCommandAsync<List<Lesson>>("GetLessons");

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Contains(response.Data, l => l.Name == "RefLesson");
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
            // BossOnly flows through the socket command's API model so the admin challenge
            // editor can restrict a boss-only statistic's target-entity picker to bosses.
            var bossesDefeated = Assert.Single(response.Data, s => s.Id == EStatisticType.BossesDefeated);
            Assert.True(bossesDefeated.BossOnly);
            Assert.All(response.Data.Where(s => s.Id != EStatisticType.BossesDefeated), s => Assert.False(s.BossOnly));
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
                ["GetAttributes", "GetChallengeTypes", "GetChallenges", "GetClasses", "GetEnemies", "GetItemMods",
                 "GetItems", "GetLessons", "GetPaths", "GetProficiencies", "GetSkillRecipes", "GetSkills", "GetStatisticTypes", "GetZones"],
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
