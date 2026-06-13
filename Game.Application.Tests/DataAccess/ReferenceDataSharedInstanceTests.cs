using Game.Abstractions.DataAccess;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Pins the per-battle allocation reduction (#449): the gameplay reference reads return the cache's
    /// shared, pre-materialized instances rather than rebuilding a fresh graph (or copying the list) on every
    /// call. Each read is exercised through the DI-resolved repository against the eagerly-loaded snapshot.
    /// </summary>
    [Collection("Integration")]
    public class ReferenceDataSharedInstanceTests : ApplicationIntegrationTestBase
    {
        public ReferenceDataSharedInstanceTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task GetItem_ReturnsSharedInstanceAndCorrectData()
        {
            int itemId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                itemId = (await TestDataSeeder.CreateItemAsync(context, name: "Shared Item")).Id;
            }

            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var items = scope.ServiceProvider.GetRequiredService<IItems>();

            var first = items.GetItem(itemId);
            var second = items.GetItem(itemId);

            // The optimization: repeated reads hand back the same pre-materialized instance, not a fresh graph.
            Assert.Same(first, second);
            Assert.Equal(itemId, first.Id);
            Assert.Equal("Shared Item", first.Name);
            Assert.NotEmpty(first.Attributes);
        }

        [Fact]
        public async Task GetItemMod_ReturnsSharedInstanceAndCorrectData()
        {
            int modId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                modId = (await TestDataSeeder.CreateItemModAsync(context, name: "Shared Mod")).Id;
            }

            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var itemMods = scope.ServiceProvider.GetRequiredService<IItemMods>();

            var first = itemMods.GetItemMod(modId);
            var second = itemMods.GetItemMod(modId);

            Assert.Same(first, second);
            Assert.Equal(modId, first.Id);
            Assert.Equal("Shared Mod", first.Name);
            Assert.NotEmpty(first.Attributes);
        }

        [Fact]
        public async Task GetSkill_ReturnsSharedInstanceAndCorrectData()
        {
            int skillId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                skillId = (await TestDataSeeder.CreateSkillAsync(context, name: "Shared Skill")).Id;
            }

            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var skills = scope.ServiceProvider.GetRequiredService<ISkills>();

            var first = skills.GetSkill(skillId);
            var second = skills.GetSkill(skillId);

            Assert.Same(first, second);
            Assert.Equal(skillId, first.Id);
            Assert.Equal("Shared Skill", first.Name);
            Assert.NotEmpty(first.DamageMultipliers);
        }

        [Fact]
        public async Task ChallengesAll_ReturnsSnapshotDirectlyWithoutCopying()
        {
            int challengeId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                challengeId = (await TestDataSeeder.CreateChallengeAsync(context)).Id;
            }

            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var challenges = scope.ServiceProvider.GetRequiredService<IChallenges>();

            var first = challenges.All();
            var second = challenges.All();

            // The optimization: All() hands back the immutable snapshot itself, not a fresh copy per call.
            Assert.Same(first, second);
            Assert.Equal(challengeId, Assert.Single(first).Id);
        }
    }
}
