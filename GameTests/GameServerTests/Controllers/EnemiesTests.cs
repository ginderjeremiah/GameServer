using GameCore;
using GameCore.Entities.Enemies;
using GameServer.Models.Common;
using GameTests.Mocks.DataAccess.Repositories;
using GameTests.Mocks.GameServer;
using static System.Net.HttpStatusCode;
using EnemyModel = GameServer.Models.Enemies.Enemy;

namespace GameTests.GameServerTests.Controllers
{
    [TestClass]
    public class EnemiesTests
    {
        [TestMethod]
        public async Task Enemies_StandardRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var enemies = ((MockEnemies)app.Repositories.Enemies).Enemies;
            enemies.Add(new Enemy { EnemyId = 1, EnemyName = "Test1", EnemyDrops = new(), SkillPool = new(), AttributeDistribution = new() });

            var response = await client.GetAsync("/api/Enemies");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiListResponse<EnemyModel>>();

            Assert.IsNotNull(data?.Data);
            Assert.AreEqual(enemies.Count, data.Data.Count);
        }

        [TestMethod]
        public async Task DefeatEnemy_StandardRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var enemies = ((MockEnemies)app.Repositories.Enemies).Enemies;
            enemies.Add(new Enemy { EnemyId = 1, EnemyName = "Test1", EnemyDrops = new(), SkillPool = new(), AttributeDistribution = new() });

            var response = await client.GetAsync("/api/Enemies");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiListResponse<EnemyModel>>();

            Assert.IsNotNull(data?.Data);
            Assert.AreEqual(enemies.Count, data.Data.Count);
        }
    }
}
