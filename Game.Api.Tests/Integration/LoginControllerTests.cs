using Game.Api.Models.Common;
using Game.Api.Models.Player;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class LoginControllerTests : ApiIntegrationTestBase
    {
        public LoginControllerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsPlayerData()
        {
            // Arrange
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "loginuser", "loginpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            var creds = new { Username = "loginuser", Password = "loginpass" };

            // Act
            var response = await Client.PostAsJsonAsync("/api/Login", creds, CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PlayerData>>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.Data);
            Assert.Equal(player.Name, result.Data.Name);

            // Verify auth cookie is set
            Assert.True(response.Headers.Contains("Set-Cookie"));
        }

        [Fact]
        public async Task Login_InvalidUsername_ReturnsError()
        {
            var creds = new { Username = "nonexistent", Password = "whatever" };

            var response = await Client.PostAsJsonAsync("/api/Login", creds, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PlayerData>>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(result.Data);
        }

        [Fact]
        public async Task CreateAccount_ValidCredentials_Succeeds()
        {
            // Arrange — CreateAccount inserts PlayerSkills with SkillId 0, 1, 2
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await TestDataSeeder.CreateSkillAsync(context, "Skill0");
            await TestDataSeeder.CreateSkillAsync(context, "Skill1");
            await TestDataSeeder.CreateSkillAsync(context, "Skill2");

            var creds = new { Username = "newuser", Password = "newpass" };

            var response = await Client.PostAsJsonAsync("/api/Login/CreateAccount", creds, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task CreateAccount_DuplicateUsername_ReturnsError()
        {
            // Arrange — create user first
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await TestDataSeeder.CreateUserAsync(context, "duplicate", "pass");

            var creds = new { Username = "duplicate", Password = "anotherpass" };

            // Act
            var response = await Client.PostAsJsonAsync("/api/Login/CreateAccount", creds, CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task Status_Authenticated_ReturnsPlayerData()
        {
            // Arrange
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "statususer", "statuspass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);

            // Login first to create a session
            var loginCreds = new { Username = "statususer", Password = "statuspass" };
            var loginResponse = await Client.PostAsJsonAsync("/api/Login", loginCreds, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            // Extract the set-cookie header and add it to a new client
            using var authClient = Factory.CreateClient();
            var cookies = loginResponse.Headers.GetValues("Set-Cookie");
            foreach (var cookie in cookies)
            {
                authClient.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
            }

            // Act
            var response = await authClient.GetAsync("/api/Login/Status", CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PlayerData>>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.Data);
            Assert.Equal(player.Name, result.Data.Name);
        }

        [Fact]
        public async Task Status_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Login/Status", CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
