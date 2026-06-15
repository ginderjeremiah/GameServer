using Game.Abstractions.DataAccess;
using Game.Application;
using Game.Application.Services;
using Game.Core;
using Game.Core.Players;
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

            var result = await battleService.EndBattleVictory(player, state, DateTime.UtcNow);

            Assert.Null(result);
        }

        [Fact]
        public async Task EndBattleVictory_ValidTimestamp_ReturnsDefeatResult()
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

            // Backdate the battle start so the simulation's TotalMs has already elapsed,
            // making DateTime.UtcNow a valid claimed timestamp (between earliestDefeat and now).
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);

            var result = await battleService.EndBattleVictory(player, state, DateTime.UtcNow);

            Assert.NotNull(result);
            Assert.True(result.ExpReward >= 0);
            Assert.False(state.HasActiveBattle);
        }

        [Fact]
        public async Task EndBattleVictory_TimestampTooEarly_ReturnsNull()
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

            var result = await battleService.EndBattleVictory(player, state, DateTime.UtcNow);

            Assert.Null(result);
        }

        [Fact]
        public async Task EndBattleVictory_ClaimedSlightlyAheadOfServer_WithinSkewTolerance_ReturnsDefeatResult()
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

            // Backdate the battle start so the simulation's TotalMs has already elapsed (the "too early"
            // side is satisfied), isolating the future-side skew check.
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);

            // A benign client clock that leads the server by less than the skew tolerance must NOT void
            // the win — the prior zero-tolerance future check would have dropped this legitimate victory.
            var claimedTimestamp = DateTime.UtcNow.AddMilliseconds(50);
            var result = await battleService.EndBattleVictory(player, state, claimedTimestamp);

            Assert.NotNull(result);
            Assert.True(result.ExpReward >= 0);
            Assert.False(state.HasActiveBattle);
        }

        [Fact]
        public async Task EndBattleVictory_ClaimedFarAheadOfServer_BeyondSkewTolerance_ReturnsNull()
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

            // Backdate so the "too early" side is satisfied, isolating the future-side skew check.
            state.BattleStartTime = DateTime.UtcNow.AddMinutes(-10);

            // A claim far beyond the skew tolerance into the future is anti-cheat and must still be rejected,
            // and the active battle must remain so the client can re-claim with a corrected timestamp.
            var claimedTimestamp = DateTime.UtcNow.AddSeconds(2);
            var result = await battleService.EndBattleVictory(player, state, claimedTimestamp);

            Assert.Null(result);
            Assert.True(state.HasActiveBattle);
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
            await battleService.EndBattleVictory(player, state, DateTime.UtcNow);

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

            var result = await battleService.EndBattleLoss(player, state);

            Assert.True(result);
            Assert.False(state.HasActiveBattle);
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
            var defeat = await battleService.EndBattleVictory(player, state, DateTime.UtcNow);
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
        public async Task StartBattle_AbandoningAnUnresolvedBattle_RecordsAbandonedNotLost()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Both combatants wield a skill with an effectively infinite cooldown, so neither lands a hit
            // within the abandon window — the re-simulation resolves as neither a win nor a death, i.e. an
            // incomplete abandon.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "SlowPoke", baseDamage: 1m, cooldownMs: 100_000_000);
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

            // Let some wall-clock time elapse for the abandon, but far less than the skills' cooldowns, so the
            // re-simulation completes no actions and resolves as neither a win nor a death.
            state.BattleStartTime = DateTime.UtcNow.AddSeconds(-1);

            // Starting a new battle abandons the in-progress one.
            await battleService.StartBattle(player, state, zoneId: zone.Id);

            // The write-behind handler wrote the abandon to the progress cache (its source of truth); the
            // command's unit-of-work commit still runs (now a no-op for progress), then read the stats back
            // from the cache.
            await unitOfWork.CommitAsync();
            var stats = await progressRepo.GetStatistics(playerEntity.Id);

            decimal StatValue(EStatisticType type, int? entityId) =>
                stats.FirstOrDefault(s => s.Type == type && s.EntityId == entityId)?.Value ?? 0m;

            // The abandon is tracked as BattlesAbandoned (global + per-enemy) and never as a loss or a win.
            Assert.Equal(1m, StatValue(EStatisticType.BattlesAbandoned, null));
            Assert.Equal(1m, StatValue(EStatisticType.BattlesAbandoned, abandonedEnemyId));
            Assert.Equal(0m, StatValue(EStatisticType.BattlesLost, null));
            Assert.Equal(0m, StatValue(EStatisticType.BattlesWon, null));
            Assert.Equal(0m, StatValue(EStatisticType.PlayerDeaths, null));
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
    }
}
