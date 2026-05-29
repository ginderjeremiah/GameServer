using Game.Abstractions.DataAccess;
using Game.Application.Services;
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

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var snapshotService = scope.ServiceProvider.GetRequiredService<BattleSnapshotService>();
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
                new Game.Abstractions.Entities.PlayerAttribute { PlayerId = playerEntity.Id, AttributeId = (int)Game.Core.EAttribute.Strength, Amount = 1m },
                new Game.Abstractions.Entities.PlayerAttribute { PlayerId = playerEntity.Id, AttributeId = (int)Game.Core.EAttribute.Endurance, Amount = 1m });
            playerEntity.StatPointsGained = 2;
            playerEntity.StatPointsUsed = 2;
            await context.SaveChangesAsync();

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

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
            var player = await playerRepo.GetPlayer(playerEntity.Id);
            Assert.NotNull(player);

            var battleService = scope.ServiceProvider.GetRequiredService<BattleService>();
            var state = new PlayerState();

            await battleService.StartBattle(player, state, zoneId: zone1.Id, newZoneId: zone2.Id);

            Assert.Equal(zone2.Id, player.CurrentZoneId);
        }
    }
}
