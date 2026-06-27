using Game.Api.Models.Player;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Exercises the <c>SynthesizeSkill</c> socket command end-to-end (spike #1125, area B): a valid recipe
    /// unlocks the result skill and returns its id, the unlock is idempotent across a repeat synthesize, and a
    /// recipe the player can't satisfy (a missing input) is rejected as anti-cheat with no state change. The
    /// per-branch validation is pinned by the domain unit tests; this confirms the command → service → domain
    /// → write-behind wiring and the persisted unlock.
    /// </summary>
    [Collection("Integration")]
    public class SynthesizeSkillSocketTests : ApiIntegrationTestBase
    {
        private const string Username = "synthuser";
        private const string Password = "synthpass";

        public SynthesizeSkillSocketTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        private async Task<(int UserId, int PlayerId, int RecipeId, int ResultSkillId)> SeedRecipeAsync(
            bool ownsAllInputs)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context, Username, Password);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var inputA = await TestDataSeeder.CreateSkillAsync(context, name: "InputA");
            var inputB = await TestDataSeeder.CreateSkillAsync(context, name: "InputB");
            var result = await TestDataSeeder.CreateSkillAsync(context, name: "Result", acquisition: ESkillAcquisition.Synthesis);

            // The player always owns input A; input B is owned only when the recipe should be satisfiable.
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, inputA.Id, selected: false);
            if (ownsAllInputs)
            {
                await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, inputB.Id, selected: false);
            }

            var recipe = await TestDataSeeder.CreateSkillRecipeAsync(
                context, resultSkillId: result.Id, inputSkillIds: [inputA.Id, inputB.Id]);

            // The caches no longer lazily refill, so reload them to resolve the seeded skills/recipe on load.
            await ReloadReferenceCachesAsync();

            return (user.Id, player.Id, recipe.Id, result.Id);
        }

        [Fact]
        public async Task SynthesizeSkill_ValidRecipe_UnlocksResultAndReturnsItsId()
        {
            var (userId, playerId, recipeId, resultSkillId) = await SeedRecipeAsync(ownsAllInputs: true);
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId, playerId);

            var response = await socketClient.SendCommandAsync<SynthesisResult>("SynthesizeSkill", recipeId);

            Assert.Null(response.Error);
            Assert.Equal(resultSkillId, response.Data.ResultSkillId);
            await WaitForUnlockedSkillAsync(playerId, resultSkillId);
        }

        [Fact]
        public async Task SynthesizeSkill_RepeatSynthesize_IsIdempotent()
        {
            var (userId, playerId, recipeId, resultSkillId) = await SeedRecipeAsync(ownsAllInputs: true);
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId, playerId);

            await socketClient.SendCommandAsync<SynthesisResult>("SynthesizeSkill", recipeId);
            await WaitForUnlockedSkillAsync(playerId, resultSkillId);

            // Re-synthesizing an already-owned result still succeeds, leaving a single unlocked copy.
            var second = await socketClient.SendCommandAsync<SynthesisResult>("SynthesizeSkill", recipeId);

            Assert.Null(second.Error);
            Assert.Equal(resultSkillId, second.Data.ResultSkillId);
            var unlocked = (await GetPersistedPlayerAsync(playerId)).UnlockedSkills;
            Assert.Single(unlocked, s => s.SkillId == resultSkillId);
        }

        [Fact]
        public async Task SynthesizeSkill_MissingInput_IsRejectedAndUnlocksNothing()
        {
            var (userId, playerId, recipeId, resultSkillId) = await SeedRecipeAsync(ownsAllInputs: false);
            await LoginAsync(Username, Password);

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId, playerId);

            var response = await socketClient.SendCommandAsync<SynthesisResult>("SynthesizeSkill", recipeId);

            Assert.NotNull(response.Error);
            Assert.Null(response.Data.ResultSkillId);
            var unlocked = (await GetPersistedPlayerAsync(playerId)).UnlockedSkills;
            Assert.DoesNotContain(unlocked, s => s.SkillId == resultSkillId);
        }

        /// <summary>
        /// The unlock writes the cached player fire-and-forget, so poll the player snapshot until the
        /// synthesized skill lands (or fail after a short budget).
        /// </summary>
        private async Task WaitForUnlockedSkillAsync(int playerId, int skillId)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var unlocked = (await GetPersistedPlayerAsync(playerId)).UnlockedSkills;
                if (unlocked.Any(s => s.SkillId == skillId))
                {
                    return;
                }

                await Task.Delay(50, CancellationToken);
            }

            Assert.Fail("Synthesized skill did not reach the player's unlocked set.");
        }
    }
}
