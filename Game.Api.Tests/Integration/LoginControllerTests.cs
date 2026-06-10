using Game.Abstractions.Infrastructure;
using Game.Api.Models.Auth;
using Game.Api.Models.Common;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class LoginControllerTests : ApiIntegrationTestBase
    {
        public LoginControllerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsPlayerDataAndTokens()
        {
            // Arrange
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "loginuser", "loginpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            var creds = new { Username = "loginuser", Password = "loginpass" };

            // Act
            var response = await Client.PostAsJsonAsync("/api/Login", creds, CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResult>>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.Data);
            Assert.Equal(player.Name, result.Data.Player.Name);

            // Both tokens are issued in the response body (no auth cookie).
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.False(string.IsNullOrEmpty(result.Data.Tokens.AccessToken));
            Assert.False(string.IsNullOrEmpty(result.Data.Tokens.RefreshToken));
        }

        [Fact]
        public async Task Login_IssuedAccessToken_AuthenticatesProtectedEndpoint()
        {
            // Arrange
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "beareruser", "bearerpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            var (authClient, _) = await LoginAndBuildClientAsync("beareruser", "bearerpass");

            // Act — the bearer access token authenticates a protected endpoint.
            var response = await authClient.GetAsync("/api/Login/Status", CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<Models.Player.PlayerData>>(CancellationToken);
            Assert.NotNull(result?.Data);
            Assert.Equal(player.Name, result.Data.Name);
            authClient.Dispose();
        }

        [Fact]
        public async Task Login_InvalidUsername_ReturnsError()
        {
            var creds = new { Username = "nonexistent", Password = "whatever" };

            var response = await Client.PostAsJsonAsync("/api/Login", creds, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResult>>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(result.Data);
        }

        [Fact]
        public async Task Login_WrongPassword_ReturnsError()
        {
            // Arrange — a real user whose stored hash won't match the supplied password.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "wrongpassuser", "correctpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            var creds = new { Username = "wrongpassuser", Password = "wrongpass" };

            // Act
            var response = await Client.PostAsJsonAsync("/api/Login", creds, CancellationToken);

            // Assert — authentication is rejected and no tokens are issued.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResult>>(CancellationToken);
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
        public async Task Status_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Login/Status", CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ActiveSession_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Login/ActiveSession", CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task ActiveSession_NoOpenSocket_ReturnsFalse()
        {
            // Arrange — a logged-in user who has not opened a game connection.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "nosocketuser", "nosocketpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            var (authClient, _) = await LoginAndBuildClientAsync("nosocketuser", "nosocketpass");

            // Act
            var result = await GetActiveSessionAsync(authClient);

            // Assert — no live connection means no other session to warn about.
            Assert.NotNull(result?.Data);
            Assert.False(result.Data.Active);
            authClient.Dispose();
        }

        [Fact]
        public async Task ActiveSession_WithOpenSocket_ReturnsTrue()
        {
            // Arrange — a logged-in user with a live websocket connection registered.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "livesocketuser", "livesocketpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            var (authClient, _) = await LoginAndBuildClientAsync("livesocketuser", "livesocketpass");

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, user.Id);
            // Round-trip a command so the connection is fully registered (the socket-presence key is set
            // before the command listener, so any response guarantees registration completed).
            await socketClient.SendCommandAsync<object>("GetStatisticTypes");

            // Act
            var result = await GetActiveSessionAsync(authClient);

            // Assert — the open connection is reported as an active session.
            Assert.NotNull(result?.Data);
            Assert.True(result.Data.Active);
            authClient.Dispose();
        }

        private static async Task<ApiResponse<ActiveSessionResult>?> GetActiveSessionAsync(HttpClient client)
        {
            var response = await client.GetAsync("/api/Login/ActiveSession", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<ApiResponse<ActiveSessionResult>>(CancellationToken);
        }

        [Fact]
        public async Task Refresh_ValidToken_RotatesAndReturnsNewTokens()
        {
            // Arrange
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "refreshuser", "refreshpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            var login = await LoginAsync("refreshuser", "refreshpass");

            // Act — exchange the refresh token for a new pair.
            var refreshResponse = await Client.PostAsJsonAsync("/api/Login/Refresh",
                new { RefreshToken = login.Tokens.RefreshToken }, CancellationToken);

            // Assert — a fresh, rotated pair is returned.
            Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
            var refreshed = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<AuthTokens>>(CancellationToken);
            Assert.NotNull(refreshed?.Data);
            Assert.False(string.IsNullOrEmpty(refreshed.Data.AccessToken));
            Assert.False(string.IsNullOrEmpty(refreshed.Data.RefreshToken));
            Assert.NotEqual(login.Tokens.RefreshToken, refreshed.Data.RefreshToken);

            // The new access token authenticates a protected endpoint.
            using var authClient = Factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.Data.AccessToken);
            var statusResponse = await authClient.GetAsync("/api/Login/Status", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        }

        [Fact]
        public async Task Refresh_SameTokenTwice_IsRejectedSecondTime()
        {
            // Arrange
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "rotateuser", "rotatepass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            var login = await LoginAsync("rotateuser", "rotatepass");

            // Act — first use succeeds, replaying the same (now consumed) token fails.
            var first = await Client.PostAsJsonAsync("/api/Login/Refresh",
                new { RefreshToken = login.Tokens.RefreshToken }, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            var second = await Client.PostAsJsonAsync("/api/Login/Refresh",
                new { RefreshToken = login.Tokens.RefreshToken }, CancellationToken);

            // Assert — single-use rotation means the original token is no longer valid.
            Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
            var result = await second.Content.ReadFromJsonAsync<ApiResponse<AuthTokens>>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task Refresh_InvalidToken_ReturnsError()
        {
            var response = await Client.PostAsJsonAsync("/api/Login/Refresh",
                new { RefreshToken = "not-a-real-token" }, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<AuthTokens>>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task Logout_Authenticated_RevokesRefreshTokenAndEndsSession()
        {
            // Arrange — a logged-in user carrying a valid access token.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "logoutuser", "logoutpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            var (authClient, login) = await LoginAndBuildClientAsync("logoutuser", "logoutpass");

            // Act
            var response = await authClient.PostAsJsonAsync("/api/Login/Logout",
                new { RefreshToken = login.Tokens.RefreshToken }, CancellationToken);

            // Assert — logout succeeds, the session is cleared, and the refresh token is revoked.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            var session = await cache.Get($"Session_{user.Id}");
            Assert.Null(session);

            // The revoked refresh token can no longer be exchanged for new tokens.
            var refreshResponse = await Client.PostAsJsonAsync("/api/Login/Refresh",
                new { RefreshToken = login.Tokens.RefreshToken }, CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, refreshResponse.StatusCode);
            authClient.Dispose();
        }

        [Fact]
        public async Task Logout_Unauthenticated_Succeeds()
        {
            // Logout is AllowAnonymous so it always succeeds, even without a valid session/token.
            var response = await Client.PostAsJsonAsync("/api/Login/Logout",
                new { RefreshToken = "irrelevant" }, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task Login_AdminUser_InjectsRoleIntoTokenAndGrantsAdminAccess()
        {
            // Arrange — a user granted the seeded Admin role.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "adminlogin", "adminpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();
            await TestDataSeeder.AssignRoleToUserAsync(context, user.Id, ERole.Admin);

            // Act — log in and reuse the issued access token against an admin endpoint.
            var (authClient, _) = await LoginAndBuildClientAsync("adminlogin", "adminpass");

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditTags", Array.Empty<object>(), CancellationToken);

            // Assert — the role baked into the token grants access (no 401/403).
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            authClient.Dispose();
        }

        [Fact]
        public async Task Login_NonAdminUser_DoesNotGrantAdminAccess()
        {
            // Arrange — a user without any roles.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "plainlogin", "plainpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            // Act
            var (authClient, _) = await LoginAndBuildClientAsync("plainlogin", "plainpass");

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditTags", Array.Empty<object>(), CancellationToken);

            // Assert — authenticated, but lacking the Admin role.
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            authClient.Dispose();
        }
    }
}
