using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Contracts = Game.Abstractions.Contracts;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Exercises <see cref="IAdminChallenges"/> <c>SaveChallenges</c>: the zero-based-id Edit-existence
    /// rejection (an out-of-range id is a not-found rejection, not an EF 0-row update), the delete-not-supported
    /// guard, the duplicate-key rejection, and a successful Add/Edit round-trip. Seeding, the admin write, and
    /// the assertion each use a separate DI scope so the write runs against an empty change tracker.
    /// </summary>
    [Collection("Integration")]
    public class AdminChallengesIntegrationTests : ApplicationIntegrationTestBase
    {
        public AdminChallengesIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public void SaveChallenges_EditOutOfRangeId_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge> { ChangeType = EChangeType.Edit, Item = NewChallenge(id: 99999) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Challenge not found.", result.ErrorMessage);
        }

        [Fact]
        public void SaveChallenges_EditNegativeId_ReturnsNotFound()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge> { ChangeType = EChangeType.Edit, Item = NewChallenge(id: -1) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Challenge not found.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveChallenges_AddAndEdit_PersistAndUpdateInPlace()
        {
            int challengeId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                challengeId = (await TestDataSeeder.CreateChallengeAsync(context, name: "Original")).Id;
            }
            await ReloadReferenceCachesAsync();

            var changes = new List<Change<Contracts.Challenge>>
            {
                new() { ChangeType = EChangeType.Add, Item = NewChallenge(name: "Brand New", progressGoal: 25m) },
                new() { ChangeType = EChangeType.Edit, Item = NewChallenge(id: challengeId, name: "Renamed", progressGoal: 50m) },
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminChallenges>();
                Assert.True(admin.SaveChallenges(changes).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var edited = await context.Challenges.AsNoTracking().SingleAsync(c => c.Id == challengeId, CancellationToken);
                Assert.Equal("Renamed", edited.Name);
                Assert.Equal(50m, edited.ProgressGoal);
                Assert.Contains(await context.Challenges.AsNoTracking().ToListAsync(CancellationToken), c => c.Name == "Brand New");
            }
        }

        [Fact]
        public async Task SaveChallenges_DeleteOfChallenge_ReturnsFailureNotSupported()
        {
            int challengeId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                challengeId = (await TestDataSeeder.CreateChallengeAsync(context)).Id;
            }
            await ReloadReferenceCachesAsync();

            // Challenges are zero-based-id reference data: a hard delete would open an Id gap, so they are
            // retired, never deleted. A Delete change is a graceful business failure rather than a throw.
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge> { ChangeType = EChangeType.Delete, Item = NewChallenge(id: challengeId) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Contains("retired, not deleted", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveChallenges_DuplicateEditKey_ReturnsFailureWithoutThrowing()
        {
            int challengeId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                challengeId = (await TestDataSeeder.CreateChallengeAsync(context)).Id;
            }
            await ReloadReferenceCachesAsync();

            // Two Edits of the same id would double-track the row and surface as an opaque EF 500 mid-batch;
            // the processor must reject the malformed batch up front as a graceful failure.
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge> { ChangeType = EChangeType.Edit, Item = NewChallenge(id: challengeId, name: "A") },
                new Change<Contracts.Challenge> { ChangeType = EChangeType.Edit, Item = NewChallenge(id: challengeId, name: "B") },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("The submitted challenge change set contains duplicate entries.", result.ErrorMessage);
        }

        [Fact]
        public void SaveChallenges_UndefinedChallengeType_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var challenge = NewChallenge(name: "Bad Type");
            challenge.ChallengeTypeId = (EChallengeType)0;

            var result = admin.SaveChallenges([new Change<Contracts.Challenge> { ChangeType = EChangeType.Add, Item = challenge }]);

            Assert.False(result.Succeeded);
            Assert.Equal("0 is not a valid challenge type.", result.ErrorMessage);
        }

        [Fact]
        public void SaveChallenges_RewardItemDoesNotExist_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge> { ChangeType = EChangeType.Add, Item = NewChallenge(name: "Bad Reward", rewardItemId: 99999) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Reward item 99999 does not exist.", result.ErrorMessage);
        }

        [Fact]
        public void SaveChallenges_RewardItemModDoesNotExist_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge> { ChangeType = EChangeType.Add, Item = NewChallenge(name: "Bad Reward Mod", rewardItemModId: 99999) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Reward item mod 99999 does not exist.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveChallenges_NewRewardItemRetired_ReturnsFailure()
        {
            int itemId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var item = await TestDataSeeder.CreateItemAsync(context, name: "Old Sword");
                item.RetiredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                await context.SaveChangesAsync(CancellationToken);
                itemId = item.Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge> { ChangeType = EChangeType.Add, Item = NewChallenge(name: "Retired Reward", rewardItemId: itemId) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Reward item 'Old Sword' is retired and cannot be newly assigned as a challenge reward.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveChallenges_NewRewardItemModRetired_ReturnsFailure()
        {
            int itemModId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var itemMod = await TestDataSeeder.CreateItemModAsync(context, name: "Old Prefix");
                itemMod.RetiredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                await context.SaveChangesAsync(CancellationToken);
                itemModId = itemMod.Id;
            }
            await ReloadReferenceCachesAsync();

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge> { ChangeType = EChangeType.Add, Item = NewChallenge(name: "Retired Reward Mod", rewardItemModId: itemModId) },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Reward item mod 'Old Prefix' is retired and cannot be newly assigned as a challenge reward.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveChallenges_EditKeepingRetiredRewardUnchanged_Succeeds()
        {
            // A reward resolves by id forever: an item retired after being authored as a challenge's reward
            // must not block later unrelated edits to that same challenge (docs/backend.md).
            int itemId, challengeId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var item = await TestDataSeeder.CreateItemAsync(context, name: "Legacy Blade");
                itemId = item.Id;
                var challenge = await TestDataSeeder.CreateChallengeAsync(context, name: "Original", rewardItemId: itemId);
                challengeId = challenge.Id;

                item.RetiredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                await context.SaveChangesAsync(CancellationToken);
            }
            await ReloadReferenceCachesAsync();

            var changes = new List<Change<Contracts.Challenge>>
            {
                new()
                {
                    ChangeType = EChangeType.Edit,
                    Item = NewChallenge(id: challengeId, name: "Renamed", rewardItemId: itemId),
                },
            };

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();
            Assert.True(admin.SaveChallenges(changes).Succeeded);
        }

        [Fact]
        public void SaveChallenges_KillsByDamageTypeWithoutTarget_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge>
                {
                    ChangeType = EChangeType.Add,
                    Item = NewChallenge(name: "No Target", challengeTypeId: EChallengeType.KillsByDamageType),
                },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("A 'Kills By Damage Type' challenge must target a damage-type key.", result.ErrorMessage);
        }

        [Fact]
        public void SaveChallenges_KillsByDamageTypeWithUndefinedTarget_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge>
                {
                    ChangeType = EChangeType.Add,
                    Item = NewChallenge(name: "Bad Damage Type", challengeTypeId: EChallengeType.KillsByDamageType, targetEntityId: 999),
                },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("999 is not a valid damage-type key.", result.ErrorMessage);
        }

        [Fact]
        public void SaveChallenges_EnemyTargetDoesNotExist_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge>
                {
                    ChangeType = EChangeType.Add,
                    Item = NewChallenge(name: "Bad Enemy Target", challengeTypeId: EChallengeType.EnemiesKilled, targetEntityId: 99999),
                },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Target enemy 99999 does not exist.", result.ErrorMessage);
        }

        [Fact]
        public void SaveChallenges_ZoneTargetDoesNotExist_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge>
                {
                    ChangeType = EChangeType.Add,
                    Item = NewChallenge(name: "Bad Zone Target", challengeTypeId: EChallengeType.ZonesCleared, targetEntityId: 99999),
                },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Target zone 99999 does not exist.", result.ErrorMessage);
        }

        [Fact]
        public void SaveChallenges_SkillTargetDoesNotExist_ReturnsFailure()
        {
            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();

            var result = admin.SaveChallenges(
            [
                new Change<Contracts.Challenge>
                {
                    ChangeType = EChangeType.Add,
                    Item = NewChallenge(name: "Bad Skill Target", challengeTypeId: EChallengeType.SkillsUsed, targetEntityId: 99999),
                },
            ]);

            Assert.False(result.Succeeded);
            Assert.Equal("Target skill 99999 does not exist.", result.ErrorMessage);
        }

        [Fact]
        public async Task SaveChallenges_TargetingRetiredEnemy_Succeeds()
        {
            // Unlike a reward, a retired target is tolerated: it can't fault the runtime the way a dangling id
            // would, and only risks an eventually-uncompletable challenge, which the content-graph lint already
            // flags post-hoc as a warning rather than a save-time rejection.
            int enemyId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var enemy = await TestDataSeeder.CreateEnemyAsync(context, name: "Ancient Dragon");
                enemy.RetiredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                await context.SaveChangesAsync(CancellationToken);
                enemyId = enemy.Id;
            }
            await ReloadReferenceCachesAsync();

            var changes = new List<Change<Contracts.Challenge>>
            {
                new()
                {
                    ChangeType = EChangeType.Add,
                    Item = NewChallenge(name: "Dragon Slayer", challengeTypeId: EChallengeType.EnemiesKilled, targetEntityId: enemyId),
                },
            };

            using var scope = CreateScope();
            var admin = scope.ServiceProvider.GetRequiredService<IAdminChallenges>();
            Assert.True(admin.SaveChallenges(changes).Succeeded);
        }

        [Fact]
        public async Task SaveChallenges_ValidDamageTypeTarget_PersistsTargetEntityId()
        {
            var changes = new List<Change<Contracts.Challenge>>
            {
                new()
                {
                    ChangeType = EChangeType.Add,
                    Item = NewChallenge(name: "Fire Hunter", challengeTypeId: EChallengeType.KillsByDamageType,
                        targetEntityId: (int)EDamageTypeKey.Fire),
                },
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminChallenges>();
                Assert.True(admin.SaveChallenges(changes).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var created = await context.Challenges.AsNoTracking().SingleAsync(c => c.Name == "Fire Hunter", CancellationToken);
                Assert.Equal((int)EDamageTypeKey.Fire, created.TargetEntityId);
            }
        }

        [Fact]
        public async Task SaveChallenges_ValidTargetAndReward_PersistsIds()
        {
            int enemyId, itemId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var enemy = await TestDataSeeder.CreateEnemyAsync(context, name: "Slime");
                var item = await TestDataSeeder.CreateItemAsync(context, name: "Slime Fang");
                enemyId = enemy.Id;
                itemId = item.Id;
            }
            await ReloadReferenceCachesAsync();

            var changes = new List<Change<Contracts.Challenge>>
            {
                new()
                {
                    ChangeType = EChangeType.Add,
                    Item = NewChallenge(name: "Slime Hunter", challengeTypeId: EChallengeType.EnemiesKilled,
                        targetEntityId: enemyId, rewardItemId: itemId),
                },
            };

            using (var writeScope = CreateScope())
            {
                var admin = writeScope.ServiceProvider.GetRequiredService<IAdminChallenges>();
                Assert.True(admin.SaveChallenges(changes).Succeeded);
                await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().CommitAsync();
            }

            using (var assertScope = CreateScope())
            {
                var context = assertScope.ServiceProvider.GetRequiredService<GameContext>();
                var created = await context.Challenges.AsNoTracking().SingleAsync(c => c.Name == "Slime Hunter", CancellationToken);
                Assert.Equal(enemyId, created.TargetEntityId);
                Assert.Equal(itemId, created.RewardItemId);
            }
        }

        private static Contracts.Challenge NewChallenge(
            int id = 0, string name = "Test Challenge", decimal progressGoal = 10m,
            EChallengeType challengeTypeId = EChallengeType.EnemiesKilled, int? targetEntityId = null,
            int? rewardItemId = null, int? rewardItemModId = null) => new()
            {
                Id = id,
                Name = name,
                Description = "",
                DesignerNotes = "",
                ChallengeTypeId = challengeTypeId,
                TargetEntityId = targetEntityId,
                ProgressGoal = progressGoal,
                RewardItemId = rewardItemId,
                RewardItemModId = rewardItemModId,
            };
    }
}
