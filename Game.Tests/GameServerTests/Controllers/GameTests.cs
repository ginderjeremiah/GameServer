using GameCore;
using GameCore.Entities.Players;
using GameServer;
using GameServer.Models.Common;
using GameServer.Models.Player;
using GameTests.Mocks.DataAccess.Repositories;
using GameTests.Mocks.GameServer;
using System.Net.Http.Json;
using static System.Net.HttpStatusCode;

namespace GameTests.GameServerTests.Controllers
{
    [TestClass]
    public class GameTests
    {
        [TestMethod]
        public async Task AdminTools_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();

            var response = await client.GetAsync("/AdminTools");

            Assert.AreEqual(Forbidden, response.StatusCode);
            Assert.IsTrue(string.IsNullOrWhiteSpace(await response.Content.ReadAsStringAsync()));
        }

        [TestMethod]
        public async Task AdminTools_StandardRequest_ReturnsOK()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);

            var response = await client.GetAsync("/AdminTools");

            Assert.AreEqual(OK, response.StatusCode);
            Assert.IsFalse(string.IsNullOrWhiteSpace(await response.Content.ReadAsStringAsync()));
        }

        [TestMethod]
        public async Task Default_StandardRequest_ReturnsOK()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();

            var response = await client.GetAsync("/");

            Assert.AreEqual(OK, response.StatusCode);
            Assert.IsFalse(string.IsNullOrWhiteSpace(await response.Content.ReadAsStringAsync()));
        }

        [TestMethod]
        public async Task Game_StandardRequest_ReturnsOK()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();

            var response = await client.GetAsync("/Game");

            Assert.AreEqual(OK, response.StatusCode);
            Assert.IsFalse(string.IsNullOrWhiteSpace(await response.Content.ReadAsStringAsync()));
        }

        [TestMethod]
        public async Task Login_IncorrectUsername_ReturnsBadRequest()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var players = ((MockPlayers)app.Repositories.Players).Players;
            var salt = Guid.NewGuid();
            players.Add(new Player { UserName = "Test", PassHash = "Password".Hash(salt.ToString()), Salt = salt });
            var payload = new LoginCredentials { Username = "IncorrectUsername", Password = "Password" };

            var response = await client.PostAsJsonAsync("/Login", payload);

            Assert.AreEqual(BadRequest, response.StatusCode);

            var data = response.Deserialize<ApiResponse<LoginData>>();

            Assert.IsNotNull(data?.Error);
            Assert.IsNull(data.Data);
        }

        [TestMethod]
        public async Task Login_IncorrectPassword_ReturnsBadRequest()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var players = ((MockPlayers)app.Repositories.Players).Players;
            var salt = Guid.NewGuid();
            players.Add(new Player { UserName = "Test", PassHash = "Password".Hash(salt.ToString()), Salt = salt });
            var payload = new LoginCredentials { Username = "Test", Password = "IncorrectPassword" };

            var response = await client.PostAsJsonAsync("/Login", payload);

            Assert.AreEqual(BadRequest, response.StatusCode);

            var data = response.Deserialize<ApiResponse<LoginData>>();

            Assert.IsNotNull(data?.Error);
            Assert.IsNull(data.Data);
        }

        [TestMethod]
        public async Task Login_StandardRequest_ReturnsOK()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var players = ((MockPlayers)app.Repositories.Players).Players;
            var salt = Guid.NewGuid();
            players.Add(new Player { UserName = "Test", PassHash = "Password".Hash(salt.ToString()), Salt = salt });
            var payload = new LoginCredentials { Username = "Test", Password = "Password" };

            var response = await client.PostAsJsonAsync("/Login", payload);

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse<LoginData>>();

            Assert.IsNotNull(data?.Data?.PlayerData);
            Assert.IsNull(data.Error);
            Assert.IsTrue(response.Headers.GetValues("Set-Cookie").Any(cookie => cookie.Contains(Constants.TOKEN_NAME)));
        }

        [TestMethod]
        public async Task Login_WithSession_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var payload = new LoginCredentials { Username = "", Password = "" };

            var response = await client.PostAsJsonAsync("/Login", payload);

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse<LoginData>>();

            Assert.IsNotNull(data?.Data?.PlayerData);
            Assert.IsNull(data.Error);
        }

        [TestMethod]
        public async Task LoginStatus_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();

            var response = await client.GetAsync("/LoginStatus");

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task LoginStatus_StandardRequest_ReturnsOK()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);

            var response = await client.GetAsync("/LoginStatus");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data?.Error);
        }

        [TestMethod]
        public async Task Test_StandardRequest_ReturnsOK()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();

            var response = await client.GetAsync("/Test");

            Assert.AreEqual(OK, response.StatusCode);
            Assert.IsFalse(string.IsNullOrWhiteSpace(await response.Content.ReadAsStringAsync()));
        }
    }
}