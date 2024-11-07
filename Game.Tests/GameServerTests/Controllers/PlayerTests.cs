using GameCore;
using GameServer;
using GameServer.Models.Attributes;
using GameServer.Models.Common;
using GameServer.Models.InventoryItems;
using GameServer.Models.Player;
using GameTests.Mocks.DataAccess.Repositories;
using GameTests.Mocks.GameServer;
using System.Net.Http.Json;
using static System.Net.HttpStatusCode;
using LogPreference = GameCore.Entities.LogPreference;
using LogPreferenceModel = GameServer.Models.Player.LogPreference;

namespace GameTests.GameServerTests.Controllers
{
    [TestClass]
    public class PlayerTests : TestsBase
    {
        [TestMethod]
        public async Task LogPreferences_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var logPrefs = (MockLogPreferences)app.Repositories.LogPreferences;
            logPrefs.LogPreferences.Add(new LogPreference { PlayerId = 1, Name = "TEST", Enabled = true });

            var response = await client.GetAsync("/api/Player/LogPreferences");

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task LogPreferences_StandardRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var playerId = app.Session.Player.PlayerId;
            var logPrefs = ((MockLogPreferences)app.Repositories.LogPreferences).LogPreferences;
            logPrefs.Add(new LogPreference { PlayerId = playerId, Name = "TEST", Enabled = true });

            var response = await client.GetAsync("/api/Player/LogPreferences");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiListResponse<LogPreferenceModel>>();

            Assert.IsNotNull(data?.Data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(logPrefs[0].Name, data.Data[0].Name);
            Assert.AreEqual(logPrefs[0].Enabled, data.Data[0].Enabled);
        }

        [TestMethod]
        public async Task Player_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();

            var response = await client.GetAsync("/api/Player");

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task Player_StandardRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);

            var response = await client.GetAsync("/api/Player");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse<PlayerData>>();

            Assert.IsNotNull(data?.Data);
            Assert.IsNull(data.Error);
            AssertObjectPropertiesAreEqual(app.Session.GetPlayerData(), data.Data);
        }

        [TestMethod]
        public async Task SaveLogPreferences_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var logPrefs = (MockLogPreferences)app.Repositories.LogPreferences;
            logPrefs.LogPreferences.Add(new LogPreference { PlayerId = 1, Name = "TEST", Enabled = true });
            var payload = new List<LogPreferenceModel>();

            var response = await client.PostAsJsonAsync("/api/Player/SaveLogPreferences", payload);

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task SaveLogPreferences_StandardRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var playerId = app.Session.Player.PlayerId;
            var logPrefs = ((MockLogPreferences)app.Repositories.LogPreferences).LogPreferences;
            logPrefs.Add(new LogPreference { PlayerId = -1, Name = "TEST2", Enabled = false });
            logPrefs.Add(new LogPreference { PlayerId = playerId, Name = "TEST1", Enabled = true });
            logPrefs.Add(new LogPreference { PlayerId = playerId, Name = "TEST2", Enabled = false });
            logPrefs.Add(new LogPreference { PlayerId = playerId, Name = "TEST3", Enabled = true });
            var payload = new List<LogPreferenceModel>
            {
                new LogPreferenceModel { Name = "TEST2", Enabled = true },
                new LogPreferenceModel { Name = "TEST3", Enabled = false },
                new LogPreferenceModel { Name = "TEST4", Enabled = true },
                new LogPreferenceModel { Name = "TEST5", Enabled = false }
            };

            var response = await client.PostAsJsonAsync("/api/Player/SaveLogPreferences", payload);

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data?.Error);

            var test0 = logPrefs.FirstOrDefault(p => p.PlayerId == -1 && p.Name == "TEST2");
            var test1 = logPrefs.FirstOrDefault(p => p.PlayerId == playerId && p.Name == "TEST1");
            var test2 = logPrefs.FirstOrDefault(p => p.PlayerId == playerId && p.Name == "TEST2");
            var test3 = logPrefs.FirstOrDefault(p => p.PlayerId == playerId && p.Name == "TEST3");
            var test4 = logPrefs.FirstOrDefault(p => p.PlayerId == playerId && p.Name == "TEST4");
            var test5 = logPrefs.FirstOrDefault(p => p.PlayerId == playerId && p.Name == "TEST5");
            Assert.AreEqual(false, test0?.Enabled);
            Assert.AreEqual(true, test1?.Enabled);
            Assert.AreEqual(true, test2?.Enabled);
            Assert.AreEqual(false, test3?.Enabled);
            Assert.AreEqual(true, test4?.Enabled);
            Assert.AreEqual(false, test5?.Enabled);
        }

        [TestMethod]
        public async Task UpdateInventorySlots_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var payload = new List<InventoryUpdate>();

            var response = await client.PostAsJsonAsync("/api/Player/UpdateInventorySlots", payload);

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task UpdateInventorySlots_StandardRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var payload = new List<InventoryUpdate>();

            var response = await client.PostAsJsonAsync("/api/Player/UpdateInventorySlots", payload);

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task UpdatePlayerStats_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var payload = new List<AttributeUpdate>();

            var response = await client.PostAsJsonAsync("/api/Player/UpdatePlayerStats", payload);

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task UpdatePlayerStats_StandardRequest_ReturnsValidData()
        {
            Assert.Fail();
        }
    }
}