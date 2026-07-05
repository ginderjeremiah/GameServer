using Game.Abstractions.DataAccess;
using Game.Application;
using Game.Application.Services;
using Game.Core;
using Game.Core.Attributes;
using Game.Core.Battle;
using Game.Core.Battle.Offline;
using Game.Core.Players;
using Game.Core.TestInfrastructure.Builders;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.Services
{
    [Collection("Integration")]
    public class BattleServiceIntegrationTests : ApplicationIntegrationTestBase
    {
        public BattleServiceIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task StartBattle_ValidZone_ReturnsBattleStartResult()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            var result = await battleService.StartBattle(player, state, zoneId: zone.Id);

            Assert.NotNull(result);
            Assert.NotNull(result.Enemy);
            Assert.True(state.HasActiveBattle);
        }

        [Fact]
        public async Task StartBattle_SnapshotsEnemyLoadoutSentToClient()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // An enemy with more skills than fit a battle loadout, so the selection actually narrows.
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            for (var i = 0; i < 6; i++)
            {
                var enemySkill = await TestDataSeeder.CreateSkillAsync(context, name: $"EnemySkill{i}");
                await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            }
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, name: "PlayerSkill");
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var enemies = scope.ServiceProvider.GetRequiredService<IEnemies>();
            var state = new PlayerState();

            var result = await battleService.StartBattle(player, state, zoneId: zone.Id);

            // The loadout is capped and snapshotted; the snapshot must equal what the client received,
            // so the server validates the battle against the exact same skills the client simulated with.
            Assert.Equal(4, result.Enemy.BattleSkills.Count);
            Assert.NotNull(state.ActiveEnemySkillIds);
            Assert.Equal(result.Enemy.BattleSkills.Select(s => s.Id), state.ActiveEnemySkillIds);

            // Reconstructing a fresh enemy from the snapshot reproduces the same loadout (the validation path).
            var reconstructed = enemies.GetDomainEnemy(result.Enemy.Id, result.Enemy.Level);
            Assert.NotNull(reconstructed);
            reconstructed.SetBattleSkills(state.ActiveEnemySkillIds);

            Assert.Equal(
                result.Enemy.BattleSkills.Select(s => s.Id),
                reconstructed.BattleSkills.Select(s => s.Id));
        }

        [Fact]
        public async Task StartBattle_InvalidZone_Throws()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => battleService.StartBattle(player, state, zoneId: 999));
        }

        [Fact]
        public async Task EndBattleVictory_NoActiveBattle_ReturnsNull()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            var result = await battleService.EndBattleVictory(player, state);

            Assert.Null(result);
        }

        [Fact]
        public async Task EndBattleVictory_EnoughTimeElapsed_ReturnsDefeatResult()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone.Id);
            Assert.True(state.HasActiveBattle);

            // Backdate the battle start so more than the simulation's TotalMs of server time has elapsed,
            // satisfying the server-measured elapsed-time check.
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);

            var result = await battleService.EndBattleVictory(player, state);

            Assert.NotNull(result);
            Assert.True(result.ExpReward >= 0);
            Assert.False(state.HasActiveBattle);
        }

        [Fact]
        public async Task EndBattleVictory_DivergentClientTotalMs_IsDiagnosticOnly_StillReturnsDefeatResult()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone.Id);
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);

            // A wildly divergent client-reported duration is logged but never gates the claim — the field
            // is diagnostic only, not anti-cheat — so the victory must still resolve to a DefeatResult.
            var result = await battleService.EndBattleVictory(player, state, clientTotalMs: int.MaxValue);

            Assert.NotNull(result);
            Assert.False(state.HasActiveBattle);
        }

        [Fact]
        public async Task EndBattleVictory_RewardMeasuresPowerFromSnapshot_NotLiveAggregate()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // A one-shot player skill makes the victory certain regardless of stats; the enemy's hit is negligible.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context,
                strengthBase: 50m, strengthPerLevel: 0m, enduranceBase: 50m, endurancePerLevel: 0m);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 1m, cooldownMs: 2000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone.Id);
            Assert.True(state.HasActiveBattle);

            // Independently compute the expected reward from the pristine snapshot before deflating live state
            // below — mirroring exactly what RecordVictory does internally, so a regression sourcing the reward
            // from the live aggregate instead of the snapshot produces a detectably different value.
            var snapshot = state.Snapshot;
            Assert.NotNull(snapshot);
            var classesRepo = scope.ServiceProvider.GetRequiredService<IClasses>();
            Game.Core.Classes.Class ResolveClass(int id) => classesRepo.GetClass(id)
                ?? throw new InvalidOperationException($"Class {id} could not be resolved.");
            var playerBattler = snapshot.ToBattler(
                scope.ServiceProvider.GetRequiredService<IItems>().GetItem,
                scope.ServiceProvider.GetRequiredService<IItemMods>().GetItemMod,
                scope.ServiceProvider.GetRequiredService<ISkills>().TryGetSkill,
                scope.ServiceProvider.GetRequiredService<IProficiencies>().GetProficiency,
                ResolveClass);
            var ratedEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(ratedEnemy);
            ratedEnemy.SelectAllBattleSkills(); // the enemy has only one authored skill ("Poke")
            var expectedReward = new DefeatRewards(playerBattler, ratedEnemy).ExpReward;

            // After the snapshot is frozen, deflate the LIVE player's power (a valid mid-battle stat
            // reallocation). The fix measures power from the snapshot, so the reward must stay at the
            // pristine-snapshot value above rather than reading (and shrinking with) the deflated live aggregate.
            foreach (var allocation in player.StatPoints.StatAllocations)
            {
                allocation.Amount = 1;
            }

            // Backdate the battle start so the simulated victory's elapsed time has already passed, making
            // DateTime.UtcNow a valid claimed timestamp.
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);

            var result = await battleService.EndBattleVictory(player, state);

            Assert.NotNull(result);
            Assert.Equal(expectedReward, result.ExpReward);
        }

        [Fact]
        public async Task RecordVictory_SnapshotsPlayerRatingOntoStats_FromSnapshotNotLiveAggregate()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Strength 50 + Endurance 50 is frozen into the battle snapshot at StartBattle.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context,
                strengthBase: 50m, strengthPerLevel: 0m, enduranceBase: 50m, endurancePerLevel: 0m);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            await ReloadReferenceCachesAsync();

            var player = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();
            await battleService.StartBattle(player, state, zoneId: zone.Id);

            // Deflate the LIVE player's power to almost nothing after the snapshot is frozen (a valid mid-battle
            // stat reallocation). The accrual must normalize by the snapshot-era rating, so a regression sourcing
            // PlayerRating from the live aggregate — or dropping the assignment — would read a far lower value.
            foreach (var allocation in player.StatPoints.StatAllocations)
            {
                allocation.Amount = 1;
            }

            var coreEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(coreEnemy);
            // RecordVictory is called directly here (bypassing TryResolveActiveBattle's SimulateBattle call), so
            // the enemy's battle loadout must be selected explicitly before DefeatRewards can rate it.
            coreEnemy.SelectAllBattleSkills();
            var result = new BattleResult(Victory: true, PlayerDied: false, TotalMs: 1000, new BattleStats());

            var rewards = battleService.RecordVictory(player, coreEnemy, result, state, DateTime.UtcNow);

            // The reward rates the snapshot-era attributes (Strength 50 + Endurance 50), not the deflated live
            // aggregate (a handful of 1s) — a live-sourced rating would be measurably lower.
            var deflatedRating = CombatRating.Rate(
                new Battler(new AttributeCollection(player.GetAllModifiers()), [], player.Level), isPlayer: true);
            Assert.True(rewards.PlayerRating > deflatedRating,
                $"Expected the snapshot-era rating ({rewards.PlayerRating}) to exceed the deflated live rating ({deflatedRating}).");
            Assert.Equal(rewards.PlayerRating, result.Stats.PlayerRating, precision: 9);
        }

        [Fact]
        public async Task RatePlayer_ReflectsLiveState_NotAFrozenBattleSnapshot()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await ReloadReferenceCachesAsync();

            var player = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var baselineRating = await battleService.RatePlayer(player);

            // A live stat reallocation (no battle involved) must move the rating on the very next call —
            // unlike DefeatRewards.PlayerRating, this display rating has no frozen snapshot to read from.
            foreach (var allocation in player.StatPoints.StatAllocations)
            {
                allocation.Amount += 50;
            }
            var bumpedRating = await battleService.RatePlayer(player);

            Assert.True(bumpedRating > baselineRating,
                $"Expected the post-reallocation rating ({bumpedRating}) to exceed the baseline ({baselineRating}).");
        }

        [Fact]
        public async Task RecordVictory_RewardIncludesClassLockedBaseInPlayerRating()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // The class's locked base (#1223) feeds the player's battle rating, so it must count in the
            // snapshot-measured CombatRating. A one-shot skill makes the victory certain.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context,
                strengthBase: 10m, strengthPerLevel: 0m, enduranceBase: 10m, endurancePerLevel: 0m);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 1m, cooldownMs: 2000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            // A class whose locked base adds Strength 50 + Endurance 50 (level-independent here) on top of the
            // seeded free pool (Strength 50 + Endurance 50).
            var lockedBaseClass = await TestDataSeeder.CreateClassWithKitAsync(context,
                starterSkillIds: [],
                attributeDistributions:
                [
                    (EAttribute.Strength, 50m, 0m),
                    (EAttribute.Endurance, 50m, 0m),
                ]);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(
                context, user.Id, zoneId: zone.Id, classId: lockedBaseClass.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();
            await battleService.StartBattle(player, state, zoneId: zone.Id);

            var coreEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(coreEnemy);
            coreEnemy.SelectAllBattleSkills();
            var result = new BattleResult(Victory: true, PlayerDied: false, TotalMs: 1000, new BattleStats());

            var rewards = battleService.RecordVictory(player, coreEnemy, result, state, DateTime.UtcNow);

            // The free-pool-only counterfactual (no locked base) must rate strictly lower than the actual
            // reward — proving the locked base is folded into the rated battler, not silently dropped.
            var resolvedSkill = scope.ServiceProvider.GetRequiredService<ISkills>().TryGetSkill(playerSkill.Id);
            Assert.NotNull(resolvedSkill);
            var freePoolOnlyRating = CombatRating.Rate(
                new Battler(new AttributeCollection(player.GetAllModifiers()), [resolvedSkill], player.Level), isPlayer: true);
            Assert.True(rewards.PlayerRating > freePoolOnlyRating,
                $"Expected the locked base to raise the rating above the free-pool-only baseline ({freePoolOnlyRating}), got {rewards.PlayerRating}.");
        }

        [Fact]
        public async Task RecordVictory_RewardIncludesClassSignaturePassiveInPlayerRating()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // The class signature passive (#1126 area E) is composed into the battler last by
            // BattleSnapshot.ToBattler, so a flat-core passive must count in the snapshot-measured CombatRating
            // exactly like the locked base. A one-shot skill makes the victory certain.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context,
                strengthBase: 10m, strengthPerLevel: 0m, enduranceBase: 10m, endurancePerLevel: 0m);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 1m, cooldownMs: 2000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            // A class with no locked-base distribution but a flat Strength 100 signature passive, on top of the
            // seeded free pool (Strength 50 + Endurance 50).
            var passiveClass = await TestDataSeeder.CreateClassAsync(context,
                passiveAttribute: EAttribute.Strength, passiveAmount: 100m);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(
                context, user.Id, zoneId: zone.Id, classId: passiveClass.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();
            await battleService.StartBattle(player, state, zoneId: zone.Id);

            var coreEnemy = scope.ServiceProvider.GetRequiredService<IEnemies>().GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(coreEnemy);
            coreEnemy.SelectAllBattleSkills();
            var result = new BattleResult(Victory: true, PlayerDied: false, TotalMs: 1000, new BattleStats());

            var rewards = battleService.RecordVictory(player, coreEnemy, result, state, DateTime.UtcNow);

            // The free-pool-only counterfactual (no signature passive) must rate strictly lower than the actual
            // reward — proving the passive is folded into the rated battler, not silently dropped.
            var resolvedSkill = scope.ServiceProvider.GetRequiredService<ISkills>().TryGetSkill(playerSkill.Id);
            Assert.NotNull(resolvedSkill);
            var freePoolOnlyRating = CombatRating.Rate(
                new Battler(new AttributeCollection(player.GetAllModifiers()), [resolvedSkill], player.Level), isPlayer: true);
            Assert.True(rewards.PlayerRating > freePoolOnlyRating,
                $"Expected the signature passive to raise the rating above the free-pool-only baseline ({freePoolOnlyRating}), got {rewards.PlayerRating}.");
        }

        [Fact]
        public async Task EndBattleVictory_ClaimedBeforeBattleCouldFinish_ReturnsNull()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone.Id);

            // The battle just started, so far less server time has elapsed than the replay's duration — the
            // claim could not physically have finished yet and is rejected (the core server-side anti-cheat).
            var result = await battleService.EndBattleVictory(player, state);

            Assert.Null(result);
        }

        [Fact]
        public async Task EndBattleVictory_CooldownAnchoredToBattleCompletion_NotServerClock()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone.Id);

            // Backdate the battle start by well over the post-battle cooldown so the battle's server-computed
            // completion (battleStart + replay duration) sits firmly in the past.
            var battleStart = DateTime.UtcNow.AddMinutes(-10);
            state.BattleStartTime = battleStart;

            var result = await battleService.EndBattleVictory(player, state);

            Assert.NotNull(result);
            // The cooldown is anchored to the battle's server-computed completion (battleStart + duration + 5s),
            // NOT to the server clock — so post-victory network latency never penalises the farm rate. With a
            // 10-minute-old start the cooldown lands in the past; a now-anchored cooldown would be in the future
            // (now + 5s). It must be at least battleStart + 5s (the replayed duration is non-negative). Read the
            // captured battleStart, not state.BattleStartTime, which ClearBattle has reset on the success path.
            Assert.True(state.EnemyCooldown < DateTime.UtcNow);
            Assert.True(state.EnemyCooldown >= battleStart.AddSeconds(5));
        }

        [Fact]
        public async Task PrepareNextIdleBattle_AnchorsBattleStartToScheduledCooldownExpiry()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone.Id);
            // Backdate so the victory resolves and anchors a cooldown.
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);
            var victory = await battleService.EndBattleVictory(player, state);
            Assert.NotNull(victory);

            // The post-battle cooldown is the deterministic scheduled start of the next fight.
            var scheduledStart = state.EnemyCooldown;

            var next = await battleService.PrepareNextIdleBattle(player, state);

            Assert.NotNull(next);
            Assert.NotNull(next.Enemy);
            Assert.True(state.HasActiveBattle);
            Assert.False(state.IsBossBattle);
            // The prefetched battle's start is anchored to the scheduled cooldown expiry — NOT to now — so the
            // next victory's elapsed-time check passes (latency only delays the claim) and the FOLLOWING
            // cooldown stays correctly sized (anchoring to now would back-date the start and shrink it).
            Assert.Equal(scheduledStart, state.BattleStartTime);
        }

        [Fact]
        public async Task TryPrepareNextIdleBattle_PrefetchFails_SwallowsAndLeavesResolvedStateCleared()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            // Win and resolve a battle: EndBattleVictory durably credits the win and clears the in-flight battle.
            await battleService.StartBattle(player, state, zoneId: zone.Id);
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);
            var victory = await battleService.EndBattleVictory(player, state);
            Assert.NotNull(victory);
            Assert.False(state.HasActiveBattle);

            // Force an unexpected prefetch failure: an out-of-range CurrentZoneId makes the prefetch's zone
            // resolution throw (GetDomainZone) after the win is already credited — exactly the window that would
            // otherwise strand the resolved state and let a reconnect re-abandon (and re-credit) the battle.
            player.ChangeZone(999);
            var next = await battleService.TryPrepareNextIdleBattle(player, state);

            // The best-effort prefetch swallows the failure (no bundled next battle) and leaves the resolved
            // (battle-cleared) state intact, so the command's SavePlayerState persists the credited outcome
            // rather than stranding a stale active battle.
            Assert.Null(next);
            Assert.False(state.HasActiveBattle);
            Assert.Null(state.ActiveEnemyId);
        }

        [Fact]
        public async Task EndBattleVictory_Success_PersistsPlayerToCache()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone.Id);
            // Backdate so the simulation's TotalMs has already elapsed
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);
            await battleService.EndBattleVictory(player, state);

            var reloadedPlayer = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(reloadedPlayer);
        }

        [Fact]
        public async Task EndBattleLoss_NoActiveBattle_ReturnsFalse()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            var result = await battleService.EndBattleLoss(player, state);

            Assert.False(result);
        }

        [Fact]
        public async Task EndBattleLoss_ValidLoss_ReturnsTrueAndClearsState()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 1m, cooldownMs: 2000);
            var strongEnemy = await TestDataSeeder.CreateStrongEnemyAsync(context);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Crush", baseDamage: 100m, cooldownMs: 500);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, strongEnemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, strongEnemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id, level: 1);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Override player attributes to be weak
            var existingAttrs = context.PlayerAttributes.Where(pa => pa.PlayerId == playerEntity.Id);
            context.PlayerAttributes.RemoveRange(existingAttrs);
            context.PlayerAttributes.AddRange(
                new Infrastructure.Entities.PlayerAttribute { PlayerId = playerEntity.Id, AttributeId = (int)Core.EAttribute.Strength, Amount = 1m },
                new Infrastructure.Entities.PlayerAttribute { PlayerId = playerEntity.Id, AttributeId = (int)Core.EAttribute.Endurance, Amount = 1m });
            playerEntity.StatPointsGained = 2;
            playerEntity.StatPointsUsed = 2;
            await context.SaveChangesAsync(CancellationToken);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone.Id);
            Assert.True(state.HasActiveBattle);

            // Backdate the battle start so more than the simulated loss's replay duration has elapsed,
            // satisfying the server-measured elapsed-time check (#1630).
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);

            var result = await battleService.EndBattleLoss(player, state);

            Assert.True(result);
            Assert.False(state.HasActiveBattle);
        }

        [Fact]
        public async Task EndBattleLoss_ClaimedBeforeBattleCouldFinish_ReturnsFalse()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 1m, cooldownMs: 2000);
            var strongEnemy = await TestDataSeeder.CreateStrongEnemyAsync(context);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Crush", baseDamage: 100m, cooldownMs: 500);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, strongEnemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, strongEnemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id, level: 1);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Override player attributes to be weak
            var existingAttrs = context.PlayerAttributes.Where(pa => pa.PlayerId == playerEntity.Id);
            context.PlayerAttributes.RemoveRange(existingAttrs);
            context.PlayerAttributes.AddRange(
                new Infrastructure.Entities.PlayerAttribute { PlayerId = playerEntity.Id, AttributeId = (int)Core.EAttribute.Strength, Amount = 1m },
                new Infrastructure.Entities.PlayerAttribute { PlayerId = playerEntity.Id, AttributeId = (int)Core.EAttribute.Endurance, Amount = 1m });
            playerEntity.StatPointsGained = 2;
            playerEntity.StatPointsUsed = 2;
            await context.SaveChangesAsync(CancellationToken);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone.Id);
            Assert.True(state.HasActiveBattle);

            // The battle just started, so far less server time has elapsed than the replay's duration — a
            // tampered client claiming an instant loss is rejected (#1630) before RecordBattleCompleted (and
            // the statistics/challenge accrual it drives) ever runs, and the battle is left active rather than
            // cleared/persisted.
            var result = await battleService.EndBattleLoss(player, state);

            Assert.False(result);
            Assert.True(state.HasActiveBattle);
        }

        [Fact]
        public async Task StartBossBattle_ZoneWithBoss_StartsDeterministicBossBattle()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // The boss gets more skills than fit a random loadout, so "full authored loadout" is observable.
            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Zone Boss", isBoss: true);
            for (var i = 0; i < 6; i++)
            {
                var bossSkill = await TestDataSeeder.CreateSkillAsync(context, name: $"BossSkill{i}");
                await TestDataSeeder.LinkSkillToEnemyAsync(context, boss.Id, bossSkill.Id);
            }
            var zone = await TestDataSeeder.CreateZoneAsync(
                context, "Boss Zone", levelMin: 1, levelMax: 3, bossEnemyId: boss.Id, bossLevel: 18);

            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, name: "PlayerSkill");
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            var result = await battleService.StartBossBattle(player, state, zone.Id);

            Assert.NotNull(result);
            Assert.Equal(boss.Id, result.Enemy.Id);
            // Deterministic: fought at the fixed boss level with its full authored loadout (no 4-skill cap).
            Assert.Equal(18, result.Enemy.Level);
            Assert.Equal(6, result.Enemy.BattleSkills.Count);
            Assert.True(state.HasActiveBattle);
            Assert.True(state.IsBossBattle);
            Assert.Equal(zone.Id, state.BattleZoneId);
            Assert.Equal(result.Enemy.BattleSkills.Select(s => s.Id), state.ActiveEnemySkillIds);
        }

        [Fact]
        public async Task StartBossBattle_ZoneWithoutBoss_ReturnsNullAndStartsNoBattle()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var zone = await TestDataSeeder.CreateZoneAsync(context, "Bossless Zone");

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            var result = await battleService.StartBossBattle(player, state, zone.Id);

            Assert.Null(result);
            Assert.False(state.HasActiveBattle);
        }

        [Fact]
        public async Task StartBossBattle_ActiveBattleInProgress_AbandonsItBeforeStarting()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var enemy = await TestDataSeeder.CreateEnemyAsync(context, "Idle Enemy");
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, name: "IdleSkill");
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Zone Boss", isBoss: true);
            var bossSkill = await TestDataSeeder.CreateSkillAsync(context, name: "BossSkill");
            await TestDataSeeder.LinkSkillToEnemyAsync(context, boss.Id, bossSkill.Id);

            var zone = await TestDataSeeder.CreateZoneAsync(
                context, "Boss Zone", bossEnemyId: boss.Id, bossLevel: 5);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, name: "PlayerSkill");
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            // Start a random idle battle, then challenge the boss without resolving it.
            await battleService.StartBattle(player, state, zoneId: zone.Id);
            Assert.False(state.IsBossBattle);

            var result = await battleService.StartBossBattle(player, state, zone.Id);

            Assert.NotNull(result);
            Assert.Equal(boss.Id, result.Enemy.Id);
            Assert.True(state.HasActiveBattle);
            Assert.True(state.IsBossBattle);
        }

        [Fact]
        public async Task StartBossBattle_BosslessZoneWithActiveBattle_ReturnsNullAndLeavesBattleUntouched()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var enemy = await TestDataSeeder.CreateEnemyAsync(context, "Idle Enemy");
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, name: "IdleSkill");
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            var bosslessZone = await TestDataSeeder.CreateZoneAsync(context, "Bossless Zone");
            await TestDataSeeder.LinkEnemyToZoneAsync(context, bosslessZone.Id, enemy.Id);

            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, name: "PlayerSkill");
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: bosslessZone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            // Start a normal idle battle in the bossless zone.
            await battleService.StartBattle(player, state, zoneId: bosslessZone.Id);
            Assert.True(state.HasActiveBattle);
            var activeEnemyId = state.ActiveEnemyId;

            // Challenging that zone's non-existent boss must be a true no-op: it returns null and leaves the
            // in-progress idle battle completely untouched (validation happens before AbandonBattle).
            var result = await battleService.StartBossBattle(player, state, bosslessZone.Id);

            Assert.Null(result);
            Assert.True(state.HasActiveBattle);
            Assert.False(state.IsBossBattle);
            Assert.Equal(activeEnemyId, state.ActiveEnemyId);
        }

        [Fact]
        public async Task StartBossBattle_ChallengingBossInDifferentZone_RecordsClearForChallengedZone()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // The player stands in one zone but challenges the dedicated boss of a *different* zone.
            var currentZone = await TestDataSeeder.CreateZoneAsync(context, "Current Zone");

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Distant Boss", isBoss: true,
                strengthBase: 1m, strengthPerLevel: 0m, enduranceBase: 1m, endurancePerLevel: 0m);
            var bossSkill = await TestDataSeeder.CreateSkillAsync(context, name: "BossPoke", baseDamage: 1m, cooldownMs: 2000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, boss.Id, bossSkill.Id);
            var bossZone = await TestDataSeeder.CreateZoneAsync(context, "Distant Zone", bossEnemyId: boss.Id, bossLevel: 1);

            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, name: "PlayerSmash", baseDamage: 1000m, cooldownMs: 500);
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: currentZone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);
            Assert.Equal(currentZone.Id, player.CurrentZoneId);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var state = new PlayerState();

            var startResult = await battleService.StartBossBattle(player, state, bossZone.Id);
            Assert.NotNull(startResult);
            Assert.Equal(bossZone.Id, state.BattleZoneId);
            // Challenging a boss does not move the player out of their current zone.
            Assert.Equal(currentZone.Id, player.CurrentZoneId);

            // Backdate so the simulated victory's elapsed time has already passed, making the claim valid.
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);
            var defeat = await battleService.EndBattleVictory(player, state);
            Assert.NotNull(defeat);

            // The write-behind handler wrote the clear to the progress cache (its source of truth); the
            // command's unit-of-work commit still runs (now a no-op for progress), then read the stats back
            // from the cache.
            await unitOfWork.CommitAsync();
            var stats = await progressRepo.GetStatistics(playerEntity.Id);

            decimal ZonesCleared(int? zoneId) =>
                stats.FirstOrDefault(s => s.Type == EStatisticType.ZonesCleared && s.EntityId == zoneId)?.Value ?? 0m;

            // The clear lands on the challenged zone (global + per-zone) — never the player's current zone.
            Assert.Equal(1m, ZonesCleared(null));
            Assert.Equal(1m, ZonesCleared(bossZone.Id));
            Assert.Equal(0m, ZonesCleared(currentZone.Id));
        }

        [Fact]
        public async Task StartBattle_AbandoningAWonBattle_GrantsExpForTheVictory()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            // Pin the encounter level so the rolled enemy yields a deterministic, non-zero exp reward:
            // the reward floors to 0 for an enemy whose total attributes are small relative to the
            // player's stat pool (DefeatRewards.GetExpReward), so a random level would make this flaky.
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 10, levelMax: 10);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone.Id);
            Assert.True(state.HasActiveBattle);

            var expBefore = player.Exp;

            // Backdate the battle start so the elapsed wall-clock time exceeds the simulated victory's
            // duration — the abandon (triggered by starting the next battle) resolves as a real win.
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);

            // Starting a new battle abandons the in-progress one. Because that one would already have been
            // won, the victory must pay out its exp (mirroring EndBattleVictory) rather than be booked
            // exp-less (#206).
            await battleService.StartBattle(player, state, zoneId: zone.Id);

            Assert.True(player.Exp > expBefore);
            Assert.True(state.HasActiveBattle);
        }

        [Fact]
        public async Task StartBattle_AbandoningAWonBattleWithOutOfRangeNewZoneId_CreditsVictoryWithoutThrowing()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            // Pin the encounter level so the rolled enemy yields a deterministic, non-zero exp reward (see
            // StartBattle_AbandoningAWonBattle_GrantsExpForTheVictory).
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 10, levelMax: 10);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone.Id);
            Assert.True(state.HasActiveBattle);

            var expBefore = player.Exp;

            // Backdate the battle start so the abandon (triggered below) resolves as a real win.
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);

            // Before the #1495 fix, a tampered out-of-range newZoneId reached GetDomainZone and threw
            // ArgumentOutOfRangeException *after* AbandonBattle had already credited the win in-memory —
            // stranding the credited-but-unsaved state (the caller's SavePlayerState never runs on a
            // fault) so a reconnect would re-abandon and re-credit the same victory. The out-of-range
            // target must now be silently ignored so the command completes normally and the win is
            // credited exactly once.
            await battleService.StartBattle(player, state, zoneId: zone.Id, newZoneId: 999);

            Assert.True(player.Exp > expBefore);
            Assert.True(state.HasActiveBattle);
            Assert.Equal(zone.Id, player.CurrentZoneId);
        }

        [Fact]
        public async Task StartBattle_AbandoningAStillInProgressBattle_HandsItBackWithoutRecordingAnOutcome()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Both combatants wield a skill with an effectively infinite cooldown, so neither lands a hit
            // within the abandon window — the re-simulation resolves as neither a win nor a death. Real elapsed
            // time since BattleStartTime stays far under the 2-minute cap, so this is not a stalemate: the
            // battle is genuinely still in progress (#1595) and must be handed back, not booked as a draw.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "SlowPoke", baseDamage: 1m, cooldownMs: 100_000_000);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "SlowSwipe", baseDamage: 1m, cooldownMs: 100_000_000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            // Pin the encounter level so the handed-back enemy is deterministic.
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone.Id);
            Assert.True(state.HasActiveBattle);
            var originalEnemyId = state.ActiveEnemyId;
            var originalSeed = state.BattleSeed;
            var expBefore = player.Exp;

            // Let a little wall-clock time elapse for the abandon, but far less than the skills' cooldowns
            // (and the 2-minute cap), so the re-simulation completes no actions and resolves as neither a win
            // nor a death.
            var battleStart = DateTime.UtcNow.AddSeconds(-1);
            state.BattleStartTime = battleStart;

            // Starting a new battle abandons the in-progress one — but since it hasn't concluded, it must be
            // handed back unchanged rather than replaced with a fresh spawn.
            var result = await battleService.StartBattle(player, state, zoneId: zone.Id);

            Assert.Equal(originalEnemyId, result.Enemy.Id);
            Assert.Equal(originalSeed, result.Seed);
            Assert.NotNull(result.ElapsedOffsetMs);
            Assert.True(result.ElapsedOffsetMs >= 900, $"Expected roughly a 1-second offset, got {result.ElapsedOffsetMs}ms.");
            Assert.True(result.ElapsedOffsetMs < GameConstants.DefaultMaxBattleMs);

            // PlayerState is left completely untouched: same enemy, same seed, same (unbackdated-further)
            // BattleStartTime.
            Assert.True(state.HasActiveBattle);
            Assert.Equal(originalEnemyId, state.ActiveEnemyId);
            Assert.Equal(originalSeed, state.BattleSeed);
            Assert.Equal(battleStart, state.BattleStartTime);

            // The write-behind handler would have written any outcome to the progress cache; the command's
            // unit-of-work commit still runs (a no-op here), then read the stats back from the cache.
            await unitOfWork.CommitAsync();
            var stats = await progressRepo.GetStatistics(playerEntity.Id);

            decimal StatValue(EStatisticType type, int? entityId) =>
                stats.FirstOrDefault(s => s.Type == type && s.EntityId == entityId)?.Value ?? 0m;

            // No outcome is recorded at all — not abandoned, not lost, not won — and no exp is granted, since
            // nothing concluded.
            Assert.Equal(0m, StatValue(EStatisticType.BattlesAbandoned, null));
            Assert.Equal(0m, StatValue(EStatisticType.BattlesLost, null));
            Assert.Equal(0m, StatValue(EStatisticType.BattlesWon, null));
            Assert.Equal(0m, StatValue(EStatisticType.PlayerDeaths, null));
            Assert.Equal(expBefore, player.Exp);
        }

        [Fact]
        public async Task ResolveStaleBattle_Concludes_ReturnsNullAndCreditsTheWin()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // A one-shot player skill concludes the replay as a win almost immediately, regardless of how
            // large the (capped) replay window is.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            // The enemy's skill carries a high base damage (but a cooldown far longer than the one-shot fight,
            // so it never actually lands) purely to give the enemy a CombatRating comparable to the player's —
            // otherwise the rating-based reward's anti-grind curve floors a capability-trivial enemy's bounty
            // to 0. The pinned encounter level keeps the reward deterministic.
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 5000m, cooldownMs: 2000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 10, levelMax: 10);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();
            var expBefore = player.Exp;

            await battleService.StartBattle(player, state, zoneId: zone.Id);
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);

            var handoff = await battleService.ResolveStaleBattle(player, state);

            Assert.Null(handoff);
            Assert.False(state.HasActiveBattle);
            Assert.True(player.Exp > expBefore);
        }

        [Fact]
        public async Task ResolveStaleBattle_StillInProgressAndGenuineDrawAtCap()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Both combatants wield a skill with an effectively infinite cooldown, so neither lands a hit no
            // matter how large the (capped) replay window is — the only thing distinguishing "still in
            // progress" from "genuine draw" is whether real elapsed time has reached the 2-minute cap.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "SlowPoke", baseDamage: 1m, cooldownMs: 100_000_000);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "SlowSwipe", baseDamage: 1m, cooldownMs: 100_000_000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();

            // Partition 1: real elapsed time is under the 2-minute cap and neither battler died — genuinely
            // still in progress. Handed back unchanged; PlayerState is left untouched.
            {
                var player = await playerRepo.GetPlayer(playerEntity.Id);
                Assert.NotNull(player);
                var state = new PlayerState();
                var expBefore = player.Exp;

                await battleService.StartBattle(player, state, zoneId: zone.Id);
                var battleStart = DateTime.UtcNow.AddSeconds(-30);
                state.BattleStartTime = battleStart;
                var enemyId = state.ActiveEnemyId;

                var handoff = await battleService.ResolveStaleBattle(player, state);

                Assert.NotNull(handoff);
                Assert.Equal(enemyId, handoff.Enemy.Id);
                Assert.NotNull(handoff.ElapsedOffsetMs);
                Assert.True(handoff.ElapsedOffsetMs is >= 29_000 and < GameConstants.DefaultMaxBattleMs);
                Assert.True(state.HasActiveBattle);
                Assert.Equal(enemyId, state.ActiveEnemyId);
                Assert.Equal(battleStart, state.BattleStartTime);
                Assert.Equal(expBefore, player.Exp);
            }

            // Partition 2: real elapsed time reaches the cap without either battler dying — a genuine draw,
            // resolved and cleared (recorded as abandoned), not handed back.
            {
                var player = await playerRepo.GetPlayer(playerEntity.Id);
                Assert.NotNull(player);
                var state = new PlayerState();
                var expBefore = player.Exp;

                await battleService.StartBattle(player, state, zoneId: zone.Id);
                state.BattleStartTime = DateTime.UtcNow.AddMinutes(-20);

                var handoff = await battleService.ResolveStaleBattle(player, state);

                Assert.Null(handoff);
                Assert.False(state.HasActiveBattle);
                Assert.Equal(expBefore, player.Exp);
            }
        }

        [Fact]
        public async Task StartBattle_SupersedingAnUnfoughtPreparedBattle_RecordsNoOutcome()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            // Pin the level so the win below yields a deterministic, non-zero exp reward.
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 10, levelMax: 10);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var state = new PlayerState();

            // Win a battle so a post-battle cooldown is set, then prefetch the next idle battle (the bundled
            // flow): it becomes the active battle with BattleStartTime anchored to the scheduled cooldown expiry.
            await battleService.StartBattle(player, state, zoneId: zone.Id);
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);
            await battleService.EndBattleVictory(player, state);
            await battleService.PrepareNextIdleBattle(player, state);
            var preparedEnemyId = state.ActiveEnemyId;
            Assert.True(state.HasActiveBattle);

            var expAfterWin = player.Exp;

            // The client never fought the prepared battle (e.g. a zone/build change during the cooldown), so it
            // supersedes with clientBattleMs: 0. Even though the scheduled start is ~10 minutes in the past
            // (a large wall-clock gap that would otherwise re-simulate and record a phantom outcome), bounding
            // the replay by the client-reported 0 ms means the abandon records nothing.
            await battleService.StartBattle(player, state, zoneId: zone.Id, clientBattleMs: 0);

            await unitOfWork.CommitAsync();
            var stats = await progressRepo.GetStatistics(playerEntity.Id);

            decimal StatValue(EStatisticType type, int? entityId) =>
                stats.FirstOrDefault(s => s.Type == type && s.EntityId == entityId)?.Value ?? 0m;

            // No phantom abandon, loss, or win for the never-fought prepared battle.
            Assert.Equal(0m, StatValue(EStatisticType.BattlesAbandoned, null));
            Assert.Equal(0m, StatValue(EStatisticType.BattlesAbandoned, preparedEnemyId));
            Assert.Equal(0m, StatValue(EStatisticType.BattlesLost, null));
            // The only win recorded is the legitimate one above — superseding the prefetch minted no phantom win.
            Assert.Equal(1m, StatValue(EStatisticType.BattlesWon, null));
            Assert.Equal(expAfterWin, player.Exp);
            // A fresh battle was started in its place.
            Assert.True(state.HasActiveBattle);
        }

        [Fact]
        public async Task StartBattle_AbandonReplayCappedAtMaxBattleDuration_KeepsTimeoutAStalemate()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // The player wields a one-shot skill whose cooldown only charges PAST the 2-minute battle cap, so
            // it never fires within a real battle — the client would draw at the cap. The enemy's skill never
            // fires either. Without clamping the abandon replay to DefaultMaxBattleMs, a long wall-clock window
            // would let the re-simulation run until the player's skill finally fires and mint a spurious win;
            // the clamp keeps the reported stalemate a draw (recorded as BattlesAbandoned, never a win).
            var playerSkill = await TestDataSeeder.CreateSkillAsync(
                context, "LateSmash", baseDamage: 100_000m, cooldownMs: GameConstants.DefaultMaxBattleMs + 60_000);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "SlowSwipe", baseDamage: 1m, cooldownMs: 100_000_000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            // Pin the encounter level so the abandoned enemy is deterministic.
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone.Id);
            Assert.True(state.HasActiveBattle);
            var abandonedEnemyId = state.ActiveEnemyId;
            var expBefore = player.Exp;

            // A wall-clock window far past the player skill's charge time: an unclamped replay would fire it
            // and resolve a win. The clamp caps the replay at the 2-minute battle duration, before it fires.
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-20);

            // Starting a new battle abandons the in-progress one.
            await battleService.StartBattle(player, state, zoneId: zone.Id);

            // The write-behind handler wrote the abandon to the progress cache (its source of truth); the
            // command's unit-of-work commit still runs (now a no-op for progress), then read the stats back.
            await unitOfWork.CommitAsync();
            var stats = await progressRepo.GetStatistics(playerEntity.Id);

            decimal StatValue(EStatisticType type, int? entityId) =>
                stats.FirstOrDefault(s => s.Type == type && s.EntityId == entityId)?.Value ?? 0m;

            // The reported stalemate stays a draw: recorded as abandoned (global + per-enemy), never a win,
            // and no exp is granted.
            Assert.Equal(1m, StatValue(EStatisticType.BattlesAbandoned, null));
            Assert.Equal(1m, StatValue(EStatisticType.BattlesAbandoned, abandonedEnemyId));
            Assert.Equal(0m, StatValue(EStatisticType.BattlesWon, null));
            Assert.Equal(expBefore, player.Exp);
        }

        [Fact]
        public async Task StartBattle_ReportedClientBattleMsUnderCapButRealElapsedPastCap_RecordsGenuineDrawNotHandoff()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Both combatants wield a skill with an effectively infinite cooldown, so no window (however
            // large) ever concludes the fight — isolating the still-in-progress-vs-draw classification to
            // real elapsed time alone.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "SlowPoke", baseDamage: 1m, cooldownMs: 100_000_000);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "SlowSwipe", baseDamage: 1m, cooldownMs: 100_000_000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone.Id);
            Assert.True(state.HasActiveBattle);
            var abandonedEnemyId = state.ActiveEnemyId;
            var abandonedSeed = state.BattleSeed;
            var expBefore = player.Exp;

            // Real elapsed time since BattleStartTime is well past the 2-minute cap...
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-3);

            // ...but the client under-reports a much smaller clientBattleMs, well under the cap. The
            // still-in-progress classification must key off the real elapsed time (wallClockMs), not this
            // client-bounded figure — otherwise a small enough report would turn an already-expired battle
            // into a false "still in progress" handoff.
            var result = await battleService.StartBattle(player, state, zoneId: zone.Id, clientBattleMs: 50_000);

            // A fresh battle was started — not a handoff of the old one (same enemy is the only one seeded in
            // the zone, so freshness is asserted via the new random seed and reset BattleStartTime instead).
            Assert.Null(result.ElapsedOffsetMs);
            Assert.NotEqual(abandonedSeed, state.BattleSeed);
            Assert.True(DateTime.UtcNow - state.BattleStartTime < TimeSpan.FromMinutes(1));

            await unitOfWork.CommitAsync();
            var stats = await progressRepo.GetStatistics(playerEntity.Id);

            decimal StatValue(EStatisticType type, int? entityId) =>
                stats.FirstOrDefault(s => s.Type == type && s.EntityId == entityId)?.Value ?? 0m;

            // The genuinely-expired battle is booked as a draw (abandoned), not silently handed back.
            Assert.Equal(1m, StatValue(EStatisticType.BattlesAbandoned, null));
            Assert.Equal(1m, StatValue(EStatisticType.BattlesAbandoned, abandonedEnemyId));
            Assert.Equal(0m, StatValue(EStatisticType.BattlesWon, null));
            Assert.Equal(expBefore, player.Exp);
        }

        [Fact]
        public async Task StartBattle_WithNewZoneId_ChangesPlayerZone()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone1 = await TestDataSeeder.CreateZoneAsync(context, "Zone 1");
            var zone2 = await TestDataSeeder.CreateZoneAsync(context, "Zone 2");
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone1.Id, enemy.Id);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone2.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone1.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone1.Id, newZoneId: zone2.Id);

            Assert.Equal(zone2.Id, player.CurrentZoneId);
        }

        [Fact]
        public async Task StartBattle_TransitionToLockedZone_KeepsPlayerInCurrentZone()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);

            // zone2 is gated behind a challenge the player has not completed, so it is locked.
            var gate = await TestDataSeeder.CreateChallengeAsync(context, "Clear Zone 1");
            var zone1 = await TestDataSeeder.CreateZoneAsync(context, "Zone 1");
            var zone2 = await TestDataSeeder.CreateZoneAsync(context, "Zone 2", unlockChallengeId: gate.Id);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone1.Id, enemy.Id);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone2.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone1.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            // A tampered request to move into the locked zone is ignored: the player stays put and the
            // battle proceeds in their current (unlocked) zone.
            var result = await battleService.StartBattle(player, state, zoneId: zone1.Id, newZoneId: zone2.Id);

            Assert.NotNull(result);
            Assert.Equal(zone1.Id, player.CurrentZoneId);
            Assert.Equal(zone1.Id, state.BattleZoneId);
        }

        [Fact]
        public async Task StartBattle_TransitionToLockedZone_AllowedOnceGatingChallengeCompleted()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);

            var gate = await TestDataSeeder.CreateChallengeAsync(context, "Clear Zone 1");
            var zone1 = await TestDataSeeder.CreateZoneAsync(context, "Zone 1");
            var zone2 = await TestDataSeeder.CreateZoneAsync(context, "Zone 2", unlockChallengeId: gate.Id);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone1.Id, enemy.Id);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone2.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone1.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);
            // The player has completed the gating challenge, so zone2 is unlocked for them.
            await TestDataSeeder.AddPlayerChallengeAsync(context, playerEntity.Id, gate.Id, progress: 1m, completed: true);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone1.Id, newZoneId: zone2.Id);

            Assert.Equal(zone2.Id, player.CurrentZoneId);
        }

        [Fact]
        public async Task StartBossBattle_LockedZone_ReturnsNullAndStartsNoBattle()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Zone Boss", isBoss: true);
            var bossSkill = await TestDataSeeder.CreateSkillAsync(context, name: "BossSkill");
            await TestDataSeeder.LinkSkillToEnemyAsync(context, boss.Id, bossSkill.Id);

            // The zone has a boss but is gated behind a challenge the player has not completed.
            var gate = await TestDataSeeder.CreateChallengeAsync(context, "Reach the boss zone");
            var zone = await TestDataSeeder.CreateZoneAsync(
                context, "Locked Boss Zone", bossEnemyId: boss.Id, bossLevel: 5, unlockChallengeId: gate.Id);

            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, name: "PlayerSkill");
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            var result = await battleService.StartBossBattle(player, state, zone.Id);

            Assert.Null(result);
            Assert.False(state.HasActiveBattle);
        }

        [Fact]
        public async Task StartBossBattle_LockedZone_AllowedOnceGatingChallengeCompleted()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Zone Boss", isBoss: true);
            var bossSkill = await TestDataSeeder.CreateSkillAsync(context, name: "BossSkill");
            await TestDataSeeder.LinkSkillToEnemyAsync(context, boss.Id, bossSkill.Id);

            var gate = await TestDataSeeder.CreateChallengeAsync(context, "Reach the boss zone");
            var zone = await TestDataSeeder.CreateZoneAsync(
                context, "Boss Zone", bossEnemyId: boss.Id, bossLevel: 5, unlockChallengeId: gate.Id);

            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, name: "PlayerSkill");
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);
            await TestDataSeeder.AddPlayerChallengeAsync(context, playerEntity.Id, gate.Id, progress: 1m, completed: true);

            // Reference data was seeded directly; reload the caches so battle setup resolves it (the caches
            // no longer lazily refill).
            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            var result = await battleService.StartBossBattle(player, state, zone.Id);

            Assert.NotNull(result);
            Assert.Equal(boss.Id, result.Enemy.Id);
            Assert.True(state.HasActiveBattle);
            Assert.True(state.IsBossBattle);
        }

        [Fact]
        public async Task StartBattle_TransitionToRetiredZone_KeepsPlayerInCurrentZone()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);

            // zone2 is retired (out of circulation), so a change into it is refused like a locked one.
            var zone1 = await TestDataSeeder.CreateZoneAsync(context, "Zone 1", order: 0);
            var zone2 = await TestDataSeeder.CreateZoneAsync(context, "Zone 2", order: 1, retiredAt: DateTime.UtcNow);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone1.Id, enemy.Id);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone2.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone1.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            // A tampered request to move into the retired zone is ignored: the player stays put and the
            // battle proceeds in their current (in-circulation) zone.
            var result = await battleService.StartBattle(player, state, zoneId: zone1.Id, newZoneId: zone2.Id);

            Assert.NotNull(result);
            Assert.Equal(zone1.Id, player.CurrentZoneId);
            Assert.Equal(zone1.Id, state.BattleZoneId);
        }

        [Fact]
        public async Task StartBattle_TransitionToOutOfRangeZone_KeepsPlayerInCurrentZone()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            // A tampered request naming an out-of-range zone id is ignored — not thrown — exactly like a
            // locked or retired target: the player stays put and the battle proceeds in their current zone.
            var result = await battleService.StartBattle(player, state, zoneId: zone.Id, newZoneId: 999);

            Assert.NotNull(result);
            Assert.Equal(zone.Id, player.CurrentZoneId);
            Assert.Equal(zone.Id, state.BattleZoneId);
        }

        [Fact]
        public async Task StartBattle_TransitionToHomeZone_KeepsPlayerInCurrentZone()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);

            // The Home zone is a no-combat sanctuary: a battle is never started in it, so a (tampered) request
            // to move a battle into Home is refused — the player's persisted zone never becomes Home, which is
            // the invariant the offline-rewards path relies on.
            var combatZone = await TestDataSeeder.CreateZoneAsync(context, "Combat", order: 0);
            var homeZone = await TestDataSeeder.CreateZoneAsync(context, "Home", order: 1, isHome: true);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, combatZone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: combatZone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            var result = await battleService.StartBattle(player, state, zoneId: combatZone.Id, newZoneId: homeZone.Id);

            Assert.NotNull(result);
            Assert.Equal(combatZone.Id, player.CurrentZoneId);
            Assert.Equal(combatZone.Id, state.BattleZoneId);
        }

        [Fact]
        public async Task StartBattle_CurrentZoneRetired_RelocatesToNearestViableZone()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);

            // The player is standing in a zone that has since been retired; the nearest viable, unlocked zone
            // (lowest Order) is where they should land.
            var viableZone = await TestDataSeeder.CreateZoneAsync(context, "Viable", order: 0);
            var retiredZone = await TestDataSeeder.CreateZoneAsync(context, "Retired", order: 1, retiredAt: DateTime.UtcNow);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, viableZone.Id, enemy.Id);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, retiredZone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: retiredZone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            var result = await battleService.StartBattle(player, state, zoneId: retiredZone.Id);

            Assert.NotNull(result);
            // The idle loop never stalls in a retired zone: the player is relocated and the battle runs there.
            Assert.Equal(viableZone.Id, player.CurrentZoneId);
            Assert.Equal(viableZone.Id, state.BattleZoneId);
        }

        [Fact]
        public async Task StartBattle_CurrentZoneHasNoSpawnableEnemies_RelocatesToNearestViableZone()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);

            // emptyZone is live but has no enemies assigned, so it has no spawn table — the runtime safety net
            // relocates rather than throwing on GetRandomDomainEnemy.
            var viableZone = await TestDataSeeder.CreateZoneAsync(context, "Viable", order: 0);
            var emptyZone = await TestDataSeeder.CreateZoneAsync(context, "Empty", order: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, viableZone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: emptyZone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            var result = await battleService.StartBattle(player, state, zoneId: emptyZone.Id);

            Assert.NotNull(result);
            Assert.Equal(viableZone.Id, player.CurrentZoneId);
            Assert.Equal(viableZone.Id, state.BattleZoneId);
        }

        [Fact]
        public async Task StartBattle_CurrentZoneOnlySpawnEnemyRetired_RelocatesToNearestViableZone()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var activeEnemy = await TestDataSeeder.CreateEnemyAsync(context, "Active");
            await TestDataSeeder.LinkSkillToEnemyAsync(context, activeEnemy.Id, skill.Id);

            // strippedZone is live, but its only assigned enemy has been retired — a retired enemy is filtered
            // out of the spawn tables, so the zone has no spawnable enemies. This is the exact #1051 trigger
            // (an admin retired every active enemy of a live zone); the runtime safety net must relocate rather
            // than throw on GetRandomDomainEnemy.
            var retiredEnemy = await TestDataSeeder.CreateEnemyAsync(context, "Retired Foe");
            retiredEnemy.RetiredAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken);

            var viableZone = await TestDataSeeder.CreateZoneAsync(context, "Viable", order: 0);
            var strippedZone = await TestDataSeeder.CreateZoneAsync(context, "Stripped", order: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, viableZone.Id, activeEnemy.Id);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, strippedZone.Id, retiredEnemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: strippedZone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            var result = await battleService.StartBattle(player, state, zoneId: strippedZone.Id);

            Assert.NotNull(result);
            Assert.Equal(viableZone.Id, player.CurrentZoneId);
            Assert.Equal(viableZone.Id, state.BattleZoneId);
        }

        [Fact]
        public async Task StartBossBattle_RetiredZone_ReturnsNullAndStartsNoBattle()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Zone Boss", isBoss: true);
            var bossSkill = await TestDataSeeder.CreateSkillAsync(context, name: "BossSkill");
            await TestDataSeeder.LinkSkillToEnemyAsync(context, boss.Id, bossSkill.Id);

            // The zone has a boss but is retired (out of circulation), so its boss cannot be challenged.
            var zone = await TestDataSeeder.CreateZoneAsync(
                context, "Retired Boss Zone", bossEnemyId: boss.Id, bossLevel: 5, retiredAt: DateTime.UtcNow);

            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, name: "PlayerSkill");
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            var result = await battleService.StartBossBattle(player, state, zone.Id);

            Assert.Null(result);
            Assert.False(state.HasActiveBattle);
        }

        [Fact]
        public async Task StartBossBattle_OutOfRangeZone_ReturnsNullAndStartsNoBattle()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, name: "PlayerSkill");
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            // A tampered ChallengeBoss request naming an out-of-range zone id is a graceful no-op — not an
            // ArgumentOutOfRangeException surfacing as an opaque 500 — like the bossless/locked/retired cases.
            var result = await battleService.StartBossBattle(player, state, 999);

            Assert.Null(result);
            Assert.False(state.HasActiveBattle);
        }

        // ── SetAutoChallengeBoss ─────────────────────────────────────────────

        [Fact]
        public async Task SetAutoChallengeBoss_EnableInBossZone_PersistsAndRoundTrips()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Zone Boss", isBoss: true);
            var zone = await TestDataSeeder.CreateZoneAsync(context, "Boss Zone", bossEnemyId: boss.Id, bossLevel: 5);

            // The player is standing in the boss zone — the boss farmed is always the current zone's boss.
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();

            var success = await battleService.SetAutoChallengeBoss(player, enabled: true);

            Assert.True(success);
            Assert.True(player.AutoChallengeBoss);

            // Round-trip through the write-behind cache (Redis is the source of truth): a fresh load
            // deserializes the persisted mode rather than reading the same in-memory instance.
            var reloaded = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(reloaded);
            Assert.True(reloaded.AutoChallengeBoss);
        }

        [Fact]
        public async Task SetAutoChallengeBoss_Disable_ReturnsToIdleAndRoundTrips()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Zone Boss", isBoss: true);
            var zone = await TestDataSeeder.CreateZoneAsync(context, "Boss Zone", bossEnemyId: boss.Id, bossLevel: 5);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();

            await battleService.SetAutoChallengeBoss(player, enabled: true);
            var success = await battleService.SetAutoChallengeBoss(player, enabled: false);

            Assert.True(success);
            Assert.False(player.AutoChallengeBoss);

            var reloaded = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(reloaded);
            Assert.False(reloaded.AutoChallengeBoss);
        }

        [Fact]
        public async Task SetAutoChallengeBoss_LockedZone_ReturnsFalseAndLeavesModeIdle()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Zone Boss", isBoss: true);
            // The current zone has a boss but is gated behind a challenge the player has not completed:
            // anti-cheat must reject enabling boss mode there.
            var gate = await TestDataSeeder.CreateChallengeAsync(context, "Reach the boss zone");
            var zone = await TestDataSeeder.CreateZoneAsync(
                context, "Locked Boss Zone", bossEnemyId: boss.Id, bossLevel: 5, unlockChallengeId: gate.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();

            var success = await battleService.SetAutoChallengeBoss(player, enabled: true);

            Assert.False(success);
            Assert.False(player.AutoChallengeBoss);
        }

        [Fact]
        public async Task SetAutoChallengeBoss_BosslessZone_ReturnsFalseAndLeavesModeIdle()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // The player's current zone has no dedicated boss, so there is nothing to boss-farm.
            var zone = await TestDataSeeder.CreateZoneAsync(context, "Bossless Zone");

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();

            var success = await battleService.SetAutoChallengeBoss(player, enabled: true);

            Assert.False(success);
            Assert.False(player.AutoChallengeBoss);
        }

        [Fact]
        public async Task SetAutoChallengeBoss_RetiredZone_ReturnsFalseAndLeavesModeIdle()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Zone Boss", isBoss: true);
            // The current zone has a boss but is retired (out of circulation): its boss cannot be farmed.
            var zone = await TestDataSeeder.CreateZoneAsync(
                context, "Retired Boss Zone", bossEnemyId: boss.Id, bossLevel: 5, retiredAt: DateTime.UtcNow);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();

            var success = await battleService.SetAutoChallengeBoss(player, enabled: true);

            Assert.False(success);
            Assert.False(player.AutoChallengeBoss);
        }

        [Fact]
        public async Task SetAutoChallengeBoss_OutOfRangeCurrentZone_ReturnsFalseAndLeavesModeIdle()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var zone = await TestDataSeeder.CreateZoneAsync(context);
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            // A stale/corrupt CurrentZoneId (e.g. after a content reseed) must be a graceful rejection, not an
            // ArgumentOutOfRangeException from resolving the domain zone.
            player.ChangeZone(999);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();

            var success = await battleService.SetAutoChallengeBoss(player, enabled: true);

            Assert.False(success);
            Assert.False(player.AutoChallengeBoss);
        }

        [Fact]
        public async Task HandBackPendingBattle_Idle_SetsActiveBattleBackdatedByElapsedOffset()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var enemiesRepo = scope.ServiceProvider.GetRequiredService<IEnemies>();
            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            var pendingEnemy = enemiesRepo.GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(pendingEnemy);
            pendingEnemy.SelectAllBattleSkills();
            var snapshot = BattleSnapshot.FromPlayer(player, []);
            var now = DateTime.UtcNow;
            var pending = new OfflinePendingBattle(pendingEnemy, Seed: 42, ElapsedOffsetMs: 45_000);

            var result = battleService.HandBackPendingBattle(state, pending, snapshot, zone.Id, isBossBattle: false, now);

            Assert.Equal(enemy.Id, result.Enemy.Id);
            Assert.Equal(42u, result.Seed);
            Assert.Equal(45_000, result.ElapsedOffsetMs);
            Assert.True(state.HasActiveBattle);
            Assert.False(state.IsBossBattle);
            Assert.Equal(zone.Id, state.BattleZoneId);
            Assert.Equal(42u, state.BattleSeed);
            Assert.Equal(now.AddMilliseconds(-45_000), state.BattleStartTime);
            Assert.Equal(pendingEnemy.BattleSkills.Select(s => s.Id), state.ActiveEnemySkillIds);
        }

        [Fact]
        public async Task HandBackPendingBattle_Boss_SetsBossFlag()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var boss = await TestDataSeeder.CreateEnemyAsync(context, "Zone Boss", isBoss: true);
            var bossSkill = await TestDataSeeder.CreateSkillAsync(context, name: "BossSkill");
            await TestDataSeeder.LinkSkillToEnemyAsync(context, boss.Id, bossSkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(
                context, "Boss Zone", levelMin: 1, levelMax: 3, bossEnemyId: boss.Id, bossLevel: 18);

            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, name: "PlayerSkill");
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, playerSkill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var enemiesRepo = scope.ServiceProvider.GetRequiredService<IEnemies>();
            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            var pendingBoss = enemiesRepo.GetDomainEnemy(boss.Id, level: 18);
            Assert.NotNull(pendingBoss);
            pendingBoss.SelectAllBattleSkills();
            var snapshot = BattleSnapshot.FromPlayer(player, []);
            var now = DateTime.UtcNow;
            var pending = new OfflinePendingBattle(pendingBoss, Seed: 7, ElapsedOffsetMs: 10_000);

            var result = battleService.HandBackPendingBattle(state, pending, snapshot, zone.Id, isBossBattle: true, now);

            Assert.Equal(boss.Id, result.Enemy.Id);
            Assert.Equal(18, result.Enemy.Level);
            Assert.True(state.HasActiveBattle);
            Assert.True(state.IsBossBattle);
            Assert.Equal(10_000, result.ElapsedOffsetMs);
            Assert.Equal(now.AddMilliseconds(-10_000), state.BattleStartTime);
        }

        [Fact]
        public async Task HandBackPendingBattle_ZeroOffset_StartsExactlyAtNow()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, skill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, playerEntity.Id, skill.Id);

            await ReloadReferenceCachesAsync();

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var enemiesRepo = scope.ServiceProvider.GetRequiredService<IEnemies>();
            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            var pendingEnemy = enemiesRepo.GetDomainEnemy(enemy.Id, level: 1);
            Assert.NotNull(pendingEnemy);
            pendingEnemy.SelectAllBattleSkills();
            var snapshot = BattleSnapshot.FromPlayer(player, []);
            var now = DateTime.UtcNow;
            var pending = new OfflinePendingBattle(pendingEnemy, Seed: 1, ElapsedOffsetMs: 0);

            var result = battleService.HandBackPendingBattle(state, pending, snapshot, zone.Id, isBossBattle: false, now);

            Assert.Equal(0, result.ElapsedOffsetMs);
            Assert.Equal(now, state.BattleStartTime);
        }
    }
}
