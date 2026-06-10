using Game.Abstractions.DataAccess;
using Game.Application.Events;
using Game.Core;
using Game.Core.Battle;
using Game.Core.Battle.Events;
using Game.Core.Players;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.Events
{
    /// <summary>
    /// Handler-level coverage for <see cref="BattleStatisticsEventHandler"/>'s challenge-reward
    /// orchestration: when a battle completes a challenge, the handler resolves the reward id via the
    /// relevant provider (<see cref="IItems"/> / <see cref="ISkills"/>; mods are unlocked by id directly)
    /// and applies the matching unlock to the player aggregate. Each reward kind is exercised the same
    /// way, alongside the additive multi-reward case and the null-reward edge (#292).
    /// </summary>
    [Collection("Integration")]
    public class BattleStatisticsEventHandlerTests : ApplicationIntegrationTestBase
    {
        public BattleStatisticsEventHandlerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task CompletingChallengeWithItemReward_UnlocksItemOnPlayer()
        {
            using var scope = CreateScope();
            var setup = await SeedScenarioAsync(scope, itemReward: true, modReward: false, skillReward: false);

            var player = await CompleteChallengeVictoryAsync(scope, setup);

            Assert.Contains(player.Inventory.UnlockedItems, u => u.ItemId == setup.ItemId);
            // The mod and skill reward ids were null, so neither kind is unlocked.
            Assert.Empty(player.Inventory.UnlockedMods);
            Assert.DoesNotContain(player.Skills, s => s.Id == setup.RewardSkillId);
        }

        [Fact]
        public async Task CompletingChallengeWithModReward_UnlocksModOnPlayer()
        {
            using var scope = CreateScope();
            var setup = await SeedScenarioAsync(scope, itemReward: false, modReward: true, skillReward: false);

            var player = await CompleteChallengeVictoryAsync(scope, setup);

            Assert.Contains(setup.ModId, player.Inventory.UnlockedMods);
            // The item and skill reward ids were null, so neither kind is unlocked.
            Assert.Empty(player.Inventory.UnlockedItems);
            Assert.DoesNotContain(player.Skills, s => s.Id == setup.RewardSkillId);
        }

        [Fact]
        public async Task CompletingChallengeWithSkillReward_UnlocksSkillUnselectedOnPlayer()
        {
            using var scope = CreateScope();
            var setup = await SeedScenarioAsync(scope, itemReward: false, modReward: false, skillReward: true);

            var player = await CompleteChallengeVictoryAsync(scope, setup);

            // Earning a skill adds it to the unlocked set but does not equip it (the loadout is chosen
            // separately) — the orchestration-level complement to the Player.UnlockSkill domain test.
            Assert.Contains(player.Skills, s => s.Id == setup.RewardSkillId);
            Assert.DoesNotContain(player.SelectedSkills, s => s.Id == setup.RewardSkillId);
            // The item and mod reward ids were null, so neither kind is unlocked.
            Assert.Empty(player.Inventory.UnlockedItems);
            Assert.Empty(player.Inventory.UnlockedMods);
        }

        [Fact]
        public async Task CompletingChallengeWithMultipleRewards_AppliesAllAdditively()
        {
            using var scope = CreateScope();
            var setup = await SeedScenarioAsync(scope, itemReward: true, modReward: true, skillReward: true);

            var player = await CompleteChallengeVictoryAsync(scope, setup);

            // A single challenge carrying every reward kind unlocks all of them.
            Assert.Contains(player.Inventory.UnlockedItems, u => u.ItemId == setup.ItemId);
            Assert.Contains(setup.ModId, player.Inventory.UnlockedMods);
            Assert.Contains(player.Skills, s => s.Id == setup.RewardSkillId);
            Assert.DoesNotContain(player.SelectedSkills, s => s.Id == setup.RewardSkillId);
        }

        [Fact]
        public async Task CompletingChallengeWithNoRewards_UnlocksNothing()
        {
            using var scope = CreateScope();
            var setup = await SeedScenarioAsync(scope, itemReward: false, modReward: false, skillReward: false);

            var player = await CompleteChallengeVictoryAsync(scope, setup);

            // The challenge genuinely completed (so the unlock loop ran) — confirm the persisted progress
            // rather than relying on the absence of unlocks, which would also hold if it never completed.
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var challengeRow = await context.PlayerChallenges.AsNoTracking()
                .SingleAsync(c => c.PlayerId == setup.PlayerId, CancellationToken);
            Assert.True(challengeRow.Completed);

            // A challenge with all-null reward ids is a clean no-op for unlocks.
            Assert.Empty(player.Inventory.UnlockedItems);
            Assert.Empty(player.Inventory.UnlockedMods);
            Assert.DoesNotContain(player.Skills, s => s.Id == setup.RewardSkillId);
        }

        /// <summary>
        /// Seeds a fresh player (with one starter, equipped skill), an enemy, and one candidate of each
        /// reward kind, plus an <see cref="EChallengeType.EnemiesKilled"/> challenge whose goal of 1 a single
        /// victory satisfies. Only the requested reward kinds are attached to the challenge, so the
        /// unattached candidates must remain locked. Refreshes the static reference caches so the handler's
        /// providers resolve exactly what was seeded.
        /// </summary>
        private async Task<Setup> SeedScenarioAsync(
            IServiceScope scope, bool itemReward, bool modReward, bool skillReward)
        {
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            // A distinct, already-equipped starter skill so "unlocked unselected" is observable against a
            // non-empty selected loadout.
            var starterSkill = await TestDataSeeder.CreateSkillAsync(context, name: "Starter");
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, starterSkill.Id);

            var enemy = await TestDataSeeder.CreateEnemyAsync(context);

            var item = await TestDataSeeder.CreateItemAsync(context);
            var mod = await TestDataSeeder.CreateItemModAsync(context);
            var rewardSkill = await TestDataSeeder.CreateSkillAsync(context, name: "Reward");

            await TestDataSeeder.CreateChallengeAsync(
                context,
                challengeTypeId: EChallengeType.EnemiesKilled,
                progressGoal: 1m,
                rewardItemId: itemReward ? item.Id : null,
                rewardItemModId: modReward ? mod.Id : null,
                rewardSkillId: skillReward ? rewardSkill.Id : null);

            // Reference caches are static and not reset between tests, so refresh them to pick up the
            // rows just seeded before the handler reads through its providers.
            ReferenceCacheCleaner.InvalidateAll(scope.ServiceProvider);

            return new Setup(player.Id, enemy.Id, item.Id, mod.Id, rewardSkill.Id);
        }

        /// <summary>
        /// Loads the player, builds a victorious <see cref="BattleCompletedEvent"/> that meets the seeded
        /// challenge's goal, and runs <see cref="BattleStatisticsEventHandler"/> over it. The handler is
        /// constructed with its real injected dependencies — exactly how the domain-event dispatcher wires
        /// it — so the orchestration is exercised end to end against live providers.
        /// </summary>
        private async Task<Player> CompleteChallengeVictoryAsync(IServiceScope scope, Setup setup)
        {
            var player = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(setup.PlayerId);
            Assert.NotNull(player);

            var enemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(setup.EnemyId, level: 1);
            Assert.NotNull(enemy);

            var handler = new BattleStatisticsEventHandler(
                scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>(),
                scope.ServiceProvider.GetRequiredService<IChallenges>(),
                scope.ServiceProvider.GetRequiredService<IItems>(),
                scope.ServiceProvider.GetRequiredService<ISkills>());

            var battleEvent = new BattleCompletedEvent(
                player, enemy, Victory: true, PlayerDied: false, TotalMs: 5000,
                Stats: new BattleStats(), IsBossBattle: false, ZoneId: player.CurrentZoneId);

            await handler.HandleAsync(battleEvent, CancellationToken);
            return player;
        }

        private sealed record Setup(int PlayerId, int EnemyId, int ItemId, int ModId, int RewardSkillId);
    }
}
