﻿using GameCore;
using GameCore.Entities;
using GameServer.Models.Common;
using GameServer.Models.Enemies;
using GameTests.Mocks.DataAccess.Repositories;
using GameTests.Mocks.GameServer;
using System.Net.Http.Json;
using static GameCore.EAttribute;
using static System.Net.HttpStatusCode;
using AttributeDistribution = GameCore.Entities.AttributeDistribution;
using BattlerAttribute = GameCore.BattleSimulation.BattlerAttribute;
using BattlerAttributeModel = GameServer.Models.Attributes.BattlerAttribute;
using Enemy = GameCore.Entities.Enemy;
using EnemyInstance = GameCore.BattleSimulation.EnemyInstance;
using EnemyInstanceModel = GameServer.Models.Enemies.EnemyInstance;
using EnemyModel = GameServer.Models.Enemies.Enemy;


namespace GameTests.GameServerTests.Controllers
{
    [TestClass]
    public class EnemiesTests
    {
        [TestMethod]
        public async Task DefeatEnemy_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var enemies = ((MockEnemies)app.Repositories.Enemies).Enemies;
            enemies.Add(new Enemy { Id = 1, Name = "Test1", EnemyDrops = [], EnemySkills = [], AttributeDistributions = [] });
            var payload = new EnemyInstanceModel
            {
                Id = 1,
                Level = 1,
                Attributes = [new BattlerAttributeModel { AttributeId = Strength, Amount = 1.0m }],
                Seed = 1,
                SelectedSkills = [1]
            };

            var response = await client.PostAsJsonAsync("/api/Enemies/DefeatEnemy", payload);

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse<DefeatEnemy>>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task DefeatEnemy_StandardRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var enemies = ((MockEnemies)app.Repositories.Enemies).Enemies;
            enemies.Add(new Enemy { Id = 1, Name = "Test1", EnemyDrops = [], EnemySkills = [], AttributeDistributions = [] });
            var zones = ((MockZones)app.Repositories.Zones).Zones;
            zones.Add(new Zone { Id = 1, LevelMax = 1, LevelMin = 1, Description = "", ZoneDrops = [], Name = "", Order = 1 });
            var enemy = new EnemyInstance
            {
                Id = 1,
                Level = 1,
                Attributes = [new BattlerAttribute { AttributeId = Strength, Amount = 1.0m }],
                Seed = 1,
                SelectedSkills = [1]
            };
            app.Session.SetActiveEnemy(enemy, DateTime.UtcNow, true);
            var payload = new EnemyInstanceModel(enemy);

            var response = await client.PostAsJsonAsync("/api/Enemies/DefeatEnemy", payload);

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse<DefeatEnemy>>();

            Assert.IsNull(data?.Error);
            Assert.IsNotNull(data?.Data?.Rewards);
            Assert.IsTrue(data.Data.Cooldown > 0.0);
        }

        [TestMethod]
        public async Task NewEnemy_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var enemies = ((MockEnemies)app.Repositories.Enemies).Enemies;
            enemies.Add(new Enemy { Id = 1, Name = "Test1", EnemyDrops = [], EnemySkills = [], AttributeDistributions = [] });

            var response = await client.GetAsync("/api/Enemies/NewEnemy");

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse<NewEnemy>>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task NewEnemy_StandardRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var enemies = ((MockEnemies)app.Repositories.Enemies).Enemies;
            enemies.Add(new Enemy { Id = 1, Name = "Test1", EnemyDrops = [], EnemySkills = [new EnemySkill { EnemyId = 1, SkillId = 0 }], AttributeDistributions = [new AttributeDistribution { AttributeId = 5, BaseAmount = 1.0m }] });
            var zones = ((MockZones)app.Repositories.Zones).Zones;
            zones.Add(new Zone { Id = 1, LevelMax = 1, LevelMin = 1, Description = "", ZoneDrops = [], Name = "", Order = 1 });
            var skills = ((MockSkills)app.Repositories.Skills).Skills;
            skills.Add(new Skill { Id = 0, Name = "", Description = "", IconPath = "", BaseDamage = 1.0m, CooldownMS = 1000, SkillDamageMultipliers = [] });

            var response = await client.GetAsync("/api/Enemies/NewEnemy");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse<NewEnemy>>();

            Assert.IsNull(data?.Error);
            Assert.IsNotNull(data?.Data?.EnemyInstance);
        }

        [TestMethod]
        public async Task NewEnemy_WithNewZoneId_ReturnsValidDataAndUpdatesCurrentZone()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var enemies = ((MockEnemies)app.Repositories.Enemies).Enemies;
            enemies.Add(new Enemy { Id = 1, Name = "Test1", EnemyDrops = [], EnemySkills = [new EnemySkill { EnemyId = 1, SkillId = 0 }], AttributeDistributions = [new AttributeDistribution { AttributeId = 5, BaseAmount = 1.0m }] });
            var zones = ((MockZones)app.Repositories.Zones).Zones;
            zones.Add(new Zone { Id = 1, LevelMax = 1, LevelMin = 1, Description = "", ZoneDrops = [], Name = "", Order = 1 });
            var skills = ((MockSkills)app.Repositories.Skills).Skills;
            skills.Add(new Skill { Id = 0, Name = "", Description = "", IconPath = "", BaseDamage = 1.0m, CooldownMS = 1000, SkillDamageMultipliers = [] });
            app.Session.CurrentZone = 0;
            var newZoneId = 1;

            var response = await client.GetAsync($"/api/Enemies/NewEnemy?newZoneId={newZoneId}");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse<NewEnemy>>();

            Assert.IsNull(data?.Error);
            Assert.IsNotNull(data?.Data?.EnemyInstance);
            Assert.AreEqual(newZoneId, app.RefreshSession().CurrentZone);
        }

        [TestMethod]
        public async Task NewEnemy_ThenDefeatEnemy_ReturnsValidDataForBoth()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var enemies = ((MockEnemies)app.Repositories.Enemies).Enemies;
            enemies.Add(new Enemy { Id = 1, Name = "Test1", EnemyDrops = [], EnemySkills = [new EnemySkill { EnemyId = 1, SkillId = 0 }], AttributeDistributions = [new AttributeDistribution { AttributeId = 5, BaseAmount = 1.0m }] });
            var zones = ((MockZones)app.Repositories.Zones).Zones;
            zones.Add(new Zone { Id = 1, LevelMax = 1, LevelMin = 1, Description = "", ZoneDrops = [], Name = "", Order = 1 });
            var skills = ((MockSkills)app.Repositories.Skills).Skills;
            skills.Add(new Skill { Id = 0, Name = "", Description = "", IconPath = "", BaseDamage = 10.0m, CooldownMS = 10, SkillDamageMultipliers = [] });
            app.Session.CurrentZone = 0;
            var newZoneId = 1;

            var newEnemyResponse = await client.GetAsync($"/api/Enemies/NewEnemy?newZoneId={newZoneId}");

            Assert.AreEqual(OK, newEnemyResponse.StatusCode);

            var newEnemyData = newEnemyResponse.Deserialize<ApiResponse<NewEnemy>>();
            var enemyInstance = newEnemyData?.Data?.EnemyInstance;

            Assert.IsNull(newEnemyData?.Error);
            Assert.IsNotNull(enemyInstance);
            Assert.AreEqual(newZoneId, app.RefreshSession().CurrentZone);

            var delay = (app.Session.EarliestDefeat - DateTime.UtcNow).TotalMilliseconds;

            await Task.Delay((int)Math.Max(delay, 0.0) + 1);

            var defeatEnemyResponse = await client.PostAsJsonAsync("/api/Enemies/DefeatEnemy", enemyInstance);

            Assert.AreEqual(OK, defeatEnemyResponse.StatusCode);

            var defeatEnemyData = defeatEnemyResponse.Deserialize<ApiResponse<DefeatEnemy>>();

            Assert.IsNull(defeatEnemyData?.Error);
            Assert.IsNotNull(defeatEnemyData?.Data?.Rewards);
            Assert.IsTrue(defeatEnemyData.Data.Cooldown > 0.0);
        }

        [TestMethod]
        public async Task Enemies_StandardRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var enemies = ((MockEnemies)app.Repositories.Enemies).Enemies;
            enemies.Add(new Enemy { Id = 1, Name = "Test1", EnemyDrops = [], EnemySkills = [], AttributeDistributions = [] });

            var response = await client.GetAsync("/api/Enemies");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiListResponse<EnemyModel>>();

            Assert.IsNull(data?.Error);
            Assert.IsNotNull(data?.Data);
            Assert.AreEqual(enemies.Count, data.Data.Count);
        }
    }
}
