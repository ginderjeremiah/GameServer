using Game.Api.Models.Player;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class SetSelectedSkillsSocketTests : ApiIntegrationTestBase
    {
        private const string Username = "skillloadoutuser";
        private const string Password = "skillloadoutpass";

        public SetSelectedSkillsSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        private async Task<(int UserId, int PlayerId, int[] SkillIds)> SeedPlayerWithUnlockedSkillsAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context, Username, Password);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // Three unlocked-but-unequipped skills the player can build a loadout from.
            var skill0 = await TestDataSeeder.CreateSkillAsync(context, name: "S0");
            var skill1 = await TestDataSeeder.CreateSkillAsync(context, name: "S1");
            var skill2 = await TestDataSeeder.CreateSkillAsync(context, name: "S2");
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill0.Id, selected: false);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill1.Id, selected: false);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill2.Id, selected: false);
            // The caches no longer lazily refill, so reload them to resolve the player's skills on load.
            await ReloadReferenceCachesAsync();

            return (user.Id, player.Id, [skill0.Id, skill1.Id, skill2.Id]);
        }

        [Fact]
        public async Task SetSelectedSkills_ValidLoadout_ReplacesEquippedSetInOrder()
        {
            var (userId, playerId, skillIds) = await SeedPlayerWithUnlockedSkillsAsync();
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            // Equip skill2 first, then skill0; skill1 stays unlocked but unequipped.
            var response = await socketClient.SendCommandRawAsync("SetSelectedSkills", new[] { skillIds[2], skillIds[0] });

            Assert.Null(response.Error);

            var skills = await WaitForSelectedSkillsAsync(playerId, expectedOrderedIds: [skillIds[2], skillIds[0]]);
            Assert.True(skills.Single(s => s.SkillId == skillIds[2]).Selected);
            Assert.Equal(0, skills.Single(s => s.SkillId == skillIds[2]).Order);
            Assert.True(skills.Single(s => s.SkillId == skillIds[0]).Selected);
            Assert.Equal(1, skills.Single(s => s.SkillId == skillIds[0]).Order);
            Assert.False(skills.Single(s => s.SkillId == skillIds[1]).Selected);
        }

        [Fact]
        public async Task SetSelectedSkills_SkillNotUnlocked_ReturnsError()
        {
            var (userId, _, skillIds) = await SeedPlayerWithUnlockedSkillsAsync();
            // Logging in creates the session the WebSocket handshake requires.
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);

            // An id far beyond the player's unlocked set must be rejected (anti-cheat), not trusted.
            var response = await socketClient.SendCommandRawAsync("SetSelectedSkills", new[] { skillIds[0], 9999 });

            Assert.NotNull(response.Error);
        }

        /// <summary>
        /// The save writes the cached player fire-and-forget, so poll the player snapshot until the
        /// expected ordered loadout lands (or fail after a short budget).
        /// </summary>
        private async Task<List<UnlockedSkill>> WaitForSelectedSkillsAsync(int playerId, int[] expectedOrderedIds)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var skills = (await GetPersistedPlayerAsync(playerId)).UnlockedSkills;
                var equipped = skills
                    .Where(s => s.Selected)
                    .OrderBy(s => s.Order)
                    .Select(s => s.SkillId)
                    .ToArray();
                if (equipped.SequenceEqual(expectedOrderedIds))
                {
                    return skills;
                }

                await Task.Delay(50, CancellationToken);
            }

            Assert.Fail("Selected skills did not reach the expected loadout.");
            return [];
        }
    }
}
