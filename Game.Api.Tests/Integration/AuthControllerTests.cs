using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Api;
using Game.Api.Http;
using Game.Api.Models.Auth;
using Game.Api.Models.Common;
using Game.Core;
using Game.Core.Identity;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class AuthControllerTests : ApiIntegrationTestBase
    {
        public AuthControllerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsPlayerSummariesAndTokens()
        {
            // Arrange
            var (_, playerId) = await SeedAsync("loginuser", "loginpass");

            var creds = new { Username = "loginuser", Password = "loginpass" };

            // Act
            var response = await Client.PostAsJsonAsync("/api/Auth", creds, CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResult>>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.Data);
            // Login lists the account's characters (no player is bound until SelectPlayer).
            var summary = Assert.Single(result.Data.PlayerSummaries);
            Assert.Equal(playerId, summary.Id);
            Assert.Equal("TestPlayer", summary.Name);

            // Both tokens are issued in the response body (no auth cookie).
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.False(string.IsNullOrEmpty(result.Data.Tokens.AccessToken));
            Assert.False(string.IsNullOrEmpty(result.Data.Tokens.RefreshToken));
        }

        [Fact]
        public async Task Login_IssuedAccessToken_AuthenticatesProtectedEndpoint()
        {
            // Arrange
            await SeedAsync("beareruser", "bearerpass");

            var (authClient, _) = await LoginAndBuildClientAsync("beareruser", "bearerpass");

            // Act — the bearer access token authenticates a protected endpoint.
            var response = await authClient.GetAsync("/api/Auth/Status", CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<Models.Player.PlayerData>>(CancellationToken);
            Assert.NotNull(result?.Data);
            Assert.Equal("TestPlayer", result.Data.Name);
            authClient.Dispose();
        }

        [Fact]
        public async Task Login_InvalidUsername_ReturnsError()
        {
            var creds = new { Username = "nonexistent", Password = "whatever" };

            var response = await Client.PostAsJsonAsync("/api/Auth", creds, CancellationToken);

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
            await SeedAsync("wrongpassuser", "correctpass");

            var creds = new { Username = "wrongpassuser", Password = "wrongpass" };

            // Act
            var response = await Client.PostAsJsonAsync("/api/Auth", creds, CancellationToken);

            // Assert — authentication is rejected and no tokens are issued.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResult>>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(result.Data);
        }

        [Fact]
        public async Task Login_BannedUser_ReturnsErrorAndEstablishesNoSession()
        {
            // A banned account with otherwise-correct credentials is rejected in the auth path: no tokens
            // are issued and no session is established.
            var (userId, _) = await SeedAsync("bannedlogin", "bannedpass");

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var user = await context.Users.FindAsync([userId], CancellationToken);
                Assert.NotNull(user);
                user.BannedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(CancellationToken);
            }

            var creds = new { Username = "bannedlogin", Password = "bannedpass" };

            // Act
            var response = await Client.PostAsJsonAsync("/api/Auth", creds, CancellationToken);

            // Assert — the login is rejected with a structured error and no body data.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResult>>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(result.Data);

            // No session is established for the rejected login.
            using var verifyScope = CreateScope();
            var sessionStore = verifyScope.ServiceProvider.GetRequiredService<ISessionStore>();
            Assert.Null(await sessionStore.GetSession(userId));
        }

        [Fact]
        public async Task CreateAccount_ValidCredentials_Succeeds()
        {
            // Signup creates the account only — no character, so no class is supplied (#1256).
            var creds = new { Username = "newuser", Password = "newpass1" };

            var response = await Client.PostAsJsonAsync("/api/Auth/CreateAccount", creds, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task CreateAccount_DuplicateUsername_ReturnsError()
        {
            // Arrange — create the user first so the duplicate-username check rejects the second attempt.
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.CreateUserAsync(context, "duplicate", "pass");
            }

            var creds = new { Username = "duplicate", Password = "anotherpass1" };

            // Act
            var response = await Client.PostAsJsonAsync("/api/Auth/CreateAccount", creds, CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task CreateAccount_UsernameAtMaxLength_Succeeds()
        {
            // Exactly the 20-char column limit (UsernamePolicy.MaxLength) — the boundary that must still fit storage.
            var creds = new { Username = new string('u', UsernamePolicy.MaxLength), Password = "newpass1" };

            var response = await Client.PostAsJsonAsync("/api/Auth/CreateAccount", creds, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
        }

        [Fact]
        public async Task CreateAccount_UsernameOverMaxLength_ReturnsValidationErrorNotServerError()
        {
            // One character past the 20-char column limit — [ApiController]'s automatic model validation must
            // reject it (as a ValidationProblemDetails 400, its standard shape for a DataAnnotations failure)
            // before it ever reaches the database, rather than letting a Postgres string-truncation error
            // escape the self-commit as a 500.
            var creds = new { Username = new string('u', UsernamePolicy.MaxLength + 1), Password = "newpass1" };

            var response = await Client.PostAsJsonAsync("/api/Auth/CreateAccount", creds, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(CancellationToken);
            Assert.NotNull(problem);
            Assert.Contains(nameof(CreateAccountRequest.Username), problem.Errors.Keys);
        }

        [Theory]
        [InlineData(" ")] // whitespace-only
        [InlineData("bad\tname")] // embedded control character
        public async Task CreateAccount_InvalidUsername_ReturnsError(string username)
        {
            var creds = new { Username = username, Password = "newpass1" };

            var response = await Client.PostAsJsonAsync("/api/Auth/CreateAccount", creds, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task CreateAccount_PasswordUnderMinLength_ReturnsValidationErrorNotServerError()
        {
            // One under PasswordPolicy.MinLength — [ApiController]'s automatic model validation must reject
            // it (as a ValidationProblemDetails 400) before the request ever reaches AccountService, the same
            // way an over-max-length username is rejected above.
            var creds = new { Username = "weakpassapi", Password = "short12" };

            var response = await Client.PostAsJsonAsync("/api/Auth/CreateAccount", creds, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(CancellationToken);
            Assert.NotNull(problem);
            Assert.Contains(nameof(CreateAccountRequest.Password), problem.Errors.Keys);
        }

        [Fact]
        public async Task CreateAccount_PasswordMissingDigit_ReturnsError()
        {
            // Right length, but fails the domain-level letter+digit rule — this reaches AccountService's
            // PasswordPolicy check (unlike the too-short case above, which never gets past model validation).
            var creds = new { Username = "weakpassapi", Password = "nodigits" };

            var response = await Client.PostAsJsonAsync("/api/Auth/CreateAccount", creds, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task CreateAccount_SurroundingWhitespace_IsTrimmedAndCollidesWithExistingUsername()
        {
            // Arrange — an existing account, then a signup for a whitespace-padded variant of the same name.
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.CreateUserAsync(context, "padded", "pass");
            }

            var creds = new { Username = "  padded  ", Password = "newpass1" };

            var response = await Client.PostAsJsonAsync("/api/Auth/CreateAccount", creds, CancellationToken);

            // Normalizing before the uniqueness check means the padded variant collides with the existing
            // account rather than creating a visually-confusable duplicate.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task CreateAccount_ConcurrentDuplicate_ReturnsCleanErrorNotServerError()
        {
            var creds = new { Username = "raceuser", Password = "racepass1" };

            // Two concurrent requests racing past the existence check both reach the commit. The
            // active-username unique index lets exactly one through; the loser must surface as a clean
            // BadRequest, not the 500 the violation would otherwise raise outside the action.
            var responses = await Task.WhenAll(
                Client.PostAsJsonAsync("/api/Auth/CreateAccount", creds, CancellationToken),
                Client.PostAsJsonAsync("/api/Auth/CreateAccount", creds, CancellationToken));

            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest));
            Assert.DoesNotContain(responses, response => response.StatusCode == HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task Status_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Auth/Status", CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            // The bearer challenge writes the project's standard ApiResponse envelope rather than the
            // JWT bearer handler's default empty body.
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result?.ErrorMessage);
        }

        [Fact]
        public async Task Status_PreSelectionToken_ReturnsNoPlayerSelectedNotOpaque404()
        {
            // A pre-selection token (post-Login, pre-SelectPlayer) is a normal, documented flow state
            // (docs/backend-auth.md) — every character-select screen refresh carries one. Status must
            // surface it as its own distinguishable category, not the same 404 a genuinely unloadable
            // player gets (see the test below).
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "playerlessstatus", "pass");

            var client = Factory.CreateClient();
            TestAuthHelper.AddAuthHeader(client, user.Id);

            var response = await client.GetAsync("/api/Auth/Status", CancellationToken);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<Models.Player.PlayerData>>(CancellationToken);
            Assert.Null(result?.Data);
            Assert.NotNull(result?.ErrorMessage);
            client.Dispose();
        }

        [Fact]
        public async Task Status_PostSelectionTokenButPlayerGone_Returns404WithError()
        {
            // A post-selection token naming a player that can't be loaded (archived/deleted between
            // requests) is a genuine missing-resource failure, distinct from the pre-selection state
            // above — it must still surface as 404, not a 500. A token minted for a player id that was
            // never persisted stands in for "since deleted" without tearing down FK-linked rows.
            var (userId, _) = await SeedAsync("playergonestatus", "pass");

            var client = Factory.CreateClient();
            TestAuthHelper.AddAuthHeader(client, userId, playerId: 999_999_999);

            var response = await client.GetAsync("/api/Auth/Status", CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<Models.Player.PlayerData>>(CancellationToken);
            Assert.Null(result?.Data);
            Assert.NotNull(result?.ErrorMessage);
            client.Dispose();
        }

        [Fact]
        public async Task ActiveSession_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Auth/ActiveSession", CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result?.ErrorMessage);
        }

        [Fact]
        public async Task Status_ValidTokenWithNoSessionCache_RehydratesAndReturnsPlayer()
        {
            // A valid token with no cached session (evicted, aged out under the sliding TTL, or never
            // established on this instance) must not be reported as "not logged in" (#693). The session is
            // rehydrated from the user's player binding instead.
            var (client, userId, _) = await SeedUserWithTokenButNoSessionAsync("evictedstatus");

            var response = await client.GetAsync("/api/Auth/Status", CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<Models.Player.PlayerData>>(CancellationToken);
            Assert.NotNull(result?.Data);
            Assert.Null(result.ErrorMessage);
            Assert.Equal("TestPlayer", result.Data.Name);

            // Rehydration is in-memory only: the request resolves the player without ever writing the session
            // cache, since player-state writes belong on the socket, not this concurrent HTTP path (#937).
            await AssertSessionNotEstablishedAsync(userId);
            client.Dispose();
        }

        [Fact]
        public async Task ActiveSession_PreSelectionToken_ChecksPresenceForExplicitOwnedCharacter()
        {
            // A pre-selection token (post-Login, pre-SelectPlayer) carries no player claim at all — but
            // ActiveSession no longer needs one. The client checks presence for the character it is about
            // to select, *before* selecting it, so the check must work off an explicit target rather than
            // the token's (absent) selected player (#1518).
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "playerlessactive", "pass");
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var client = Factory.CreateClient();
            TestAuthHelper.AddAuthHeader(client, user.Id);

            var result = await GetActiveSessionAsync(client, player.Id);

            Assert.NotNull(result?.Data);
            Assert.Null(result.ErrorMessage);
            Assert.False(result.Data.Active);
            client.Dispose();
        }

        [Fact]
        public async Task ActiveSession_UnownedPlayer_ReturnsError()
        {
            // Anti-cheat: a caller may only probe presence for one of its own characters, mirroring
            // SelectPlayer's ownership check (#1518).
            var (userId, _) = await SeedAsync("checkeruser", "pass");
            var (_, otherPlayerId) = await SeedAsync("otheruser", "pass");

            var client = Factory.CreateClient();
            TestAuthHelper.AddAuthHeader(client, userId);

            var response = await client.GetAsync($"/api/Auth/ActiveSession?playerId={otherPlayerId}", CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ActiveSessionResult>>(CancellationToken);
            Assert.Null(result?.Data);
            Assert.NotNull(result?.ErrorMessage);
            client.Dispose();
        }

        [Fact]
        public async Task ActiveSession_NoSessionCacheEstablished_ChecksPresenceWithoutTouchingCache()
        {
            // ActiveSession takes an explicit playerId rather than reading the token's selected player, so
            // unlike Status it never reads or writes the session cache (#1518) — confirm that holds even for
            // a user with no cached session at all (the "valid token, evicted/absent session" state #693
            // originally pinned Status's rehydration against).
            var (client, userId, playerId) = await SeedUserWithTokenButNoSessionAsync("evictedactive");

            var result = await GetActiveSessionAsync(client, playerId);

            Assert.NotNull(result?.Data);
            Assert.Null(result.ErrorMessage);
            Assert.False(result.Data.Active);

            await AssertSessionNotEstablishedAsync(userId);
            client.Dispose();
        }

        [Fact]
        public async Task NonSessionEndpoint_ValidTokenWithNoSessionCache_DoesNotEstablishSession()
        {
            // The redundant per-request session read is gone (#755): an authenticated endpoint that never
            // reads player state (here DeviceInfo) must not load or rehydrate the session cache, even for a
            // user who has a resolvable player. Only the socket handshake and Status/ActiveSession do.
            var (client, userId, _) = await SeedUserWithTokenButNoSessionAsync("nosessionread");
            // Well-formed (64 lowercase hex chars) — ClientHints.DeviceFingerprint now rejects anything else (#2064).
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                ClientHints.DeviceFingerprintHeader, "a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9a9");

            var response = await client.PostAsJsonAsync("/api/Auth/DeviceInfo",
                new { DeviceMemory = 8.0, HardwareConcurrency = 4 }, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Confirm the session was never established — the cache stays empty for this user.
            await AssertSessionNotEstablishedAsync(userId);
            client.Dispose();
        }

        /// <summary>
        /// Seeds a user with a player (and a linked skill so the aggregate loads) and returns a client
        /// carrying a valid bearer token for that user but with no session ever established in the cache —
        /// the "valid token, evicted/absent session" state.
        /// </summary>
        private async Task<(HttpClient Client, int UserId, int PlayerId)>
            SeedUserWithTokenButNoSessionAsync(string username)
        {
            var (userId, playerId) = await SeedAsync(username, "pass");

            using (var scope = CreateScope())
            {
                var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
                Assert.Null(await sessionStore.GetSession(userId));
            }

            var client = Factory.CreateClient();
            // A post-selection token (carrying the selected-player claim) with no cached session — the
            // "valid token, evicted/absent session" state that must rehydrate from the claim.
            TestAuthHelper.AddAuthHeader(client, userId, playerId);
            return (client, userId, playerId);
        }

        // Confirms a session was never written to the cache: rehydration (and any non-session endpoint) resolves
        // the player in memory only, so we give any erroneous fire-and-forget write a window to land, then assert
        // the key stays absent for this user.
        private async Task AssertSessionNotEstablishedAsync(int userId)
        {
            await Task.Delay(250, CancellationToken);
            using var scope = CreateScope();
            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            Assert.Null(await sessionStore.GetSession(userId));
        }

        [Fact]
        public async Task ActiveSession_NoOpenSocket_ReturnsFalse()
        {
            // Arrange — a logged-in user who has not opened a game connection.
            var (_, playerId) = await SeedAsync("nosocketuser", "nosocketpass");

            var (authClient, _) = await LoginAndBuildClientAsync("nosocketuser", "nosocketpass");

            // Act
            var result = await GetActiveSessionAsync(authClient, playerId);

            // Assert — no live connection means no other session to warn about.
            Assert.NotNull(result?.Data);
            Assert.False(result.Data.Active);
            authClient.Dispose();
        }

        [Fact]
        public async Task ActiveSession_WithOpenSocket_ReturnsTrue()
        {
            // Arrange — a logged-in user with a live websocket connection registered.
            var (userId, playerId) = await SeedAsync("livesocketuser", "livesocketpass");

            var (authClient, _) = await LoginAndBuildClientAsync("livesocketuser", "livesocketpass");

            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, userId);
            // Round-trip a command so the connection is fully registered (the socket-presence key is set
            // before the command listener, so any response guarantees registration completed).
            await socketClient.SendCommandAsync<object>("GetStatisticTypes");

            // Act
            var result = await GetActiveSessionAsync(authClient, playerId);

            // Assert — the open connection is reported as an active session.
            Assert.NotNull(result?.Data);
            Assert.True(result.Data.Active);
            authClient.Dispose();
        }

        private static async Task<ApiResponse<ActiveSessionResult>?> GetActiveSessionAsync(HttpClient client, int playerId)
        {
            var response = await client.GetAsync($"/api/Auth/ActiveSession?playerId={playerId}", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<ApiResponse<ActiveSessionResult>>(CancellationToken);
        }

        [Fact]
        public async Task Refresh_ValidToken_RotatesAndReturnsNewTokens()
        {
            // Arrange
            var (_, playerId) = await SeedAsync("refreshuser", "refreshpass");

            // Select a character so the refreshed token carries the selected player (and Status can load it).
            var login = await LoginAsync("refreshuser", "refreshpass");
            var select = await SelectPlayerAsync(login.Tokens, playerId);

            // Act — exchange the refresh token for a new pair.
            var refreshResponse = await Client.PostAsJsonAsync("/api/Auth/Refresh",
                new { select.Tokens.RefreshToken }, CancellationToken);

            // Assert — a fresh, rotated pair is returned.
            Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
            var refreshed = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<AuthTokens>>(CancellationToken);
            Assert.NotNull(refreshed?.Data);
            Assert.False(string.IsNullOrEmpty(refreshed.Data.AccessToken));
            Assert.False(string.IsNullOrEmpty(refreshed.Data.RefreshToken));
            Assert.NotEqual(select.Tokens.RefreshToken, refreshed.Data.RefreshToken);

            // The new access token authenticates a protected endpoint and keeps the selected player bound.
            using var authClient = Factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.Data.AccessToken);
            var statusResponse = await authClient.GetAsync("/api/Auth/Status", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        }

        [Fact]
        public async Task Refresh_SameTokenTwice_IsRejectedSecondTime()
        {
            // Arrange
            await SeedAsync("rotateuser", "rotatepass");

            var login = await LoginAsync("rotateuser", "rotatepass");

            // Act — first use succeeds, replaying the same (now consumed) token fails.
            var first = await Client.PostAsJsonAsync("/api/Auth/Refresh",
                new { RefreshToken = login.Tokens.RefreshToken }, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            var second = await Client.PostAsJsonAsync("/api/Auth/Refresh",
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
            var response = await Client.PostAsJsonAsync("/api/Auth/Refresh",
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
            var (userId, _) = await SeedAsync("logoutuser", "logoutpass");

            var (authClient, tokens) = await LoginAndBuildClientAsync("logoutuser", "logoutpass");

            // Act
            var response = await authClient.PostAsJsonAsync("/api/Auth/Logout",
                new { tokens.RefreshToken }, CancellationToken);

            // Assert — logout succeeds, the session is cleared, and the refresh token is revoked.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);

            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            var session = await cache.Get($"Session_{userId}");
            Assert.Null(session);

            // The revoked refresh token can no longer be exchanged for new tokens.
            var refreshResponse = await Client.PostAsJsonAsync("/api/Auth/Refresh",
                new { tokens.RefreshToken }, CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, refreshResponse.StatusCode);
            authClient.Dispose();
        }

        [Fact]
        public async Task Logout_ExpiredAccessTokenButValidRefreshToken_StillEvictsSession()
        {
            // The common logout path (#906): the 15-minute access token has already expired, so the client
            // logs out anonymously with just its refresh token. No request principal means no recorded
            // UserId, yet the cached session must still be evicted — derived from the consumed refresh token.
            var (userId, playerId) = await SeedAsync("expiredlogout", "logoutpass");

            // Selecting a character establishes the cached session; the refresh token outlives the access token.
            var login = await LoginAsync("expiredlogout", "logoutpass");
            var select = await SelectPlayerAsync(login.Tokens, playerId);
            await AssertSessionPresentAsync(userId);

            // Act — log out over the unauthenticated client (no bearer token, mimicking the expired access
            // token) carrying only the still-valid refresh token.
            var response = await Client.PostAsJsonAsync("/api/Auth/Logout",
                new { select.Tokens.RefreshToken }, CancellationToken);

            // Assert — logout succeeds and the session is evicted despite the absent access token.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await AssertSessionEvictedAsync(userId);

            // The consumed refresh token can no longer be exchanged for new tokens.
            var refreshResponse = await Client.PostAsJsonAsync("/api/Auth/Refresh",
                new { select.Tokens.RefreshToken }, CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, refreshResponse.StatusCode);
        }

        [Fact]
        public async Task Logout_Unauthenticated_Succeeds()
        {
            // Logout is AllowAnonymous so it always succeeds, even without a valid session/token.
            var response = await Client.PostAsJsonAsync("/api/Auth/Logout",
                new { RefreshToken = "irrelevant" }, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
        }

        // The session store write after login is fire-and-forget, so poll until the session is cached.
        private async Task AssertSessionPresentAsync(int userId)
        {
            using var scope = CreateScope();
            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            var session = await PollingHelper.PollUntilAsync(
                () => sessionStore.GetSession(userId), s => s is not null, timeoutMs: 5000);

            Assert.True(session is not null, "The session was not established in the cache after login.");
        }

        // The Clear on logout is fire-and-forget, so poll until the session disappears.
        private async Task AssertSessionEvictedAsync(int userId)
        {
            using var scope = CreateScope();
            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            var session = await PollingHelper.PollUntilAsync(
                () => sessionStore.GetSession(userId), s => s is null, timeoutMs: 5000);

            Assert.True(session is null, "The session was not evicted from the cache on logout.");
        }

        [Fact]
        public async Task Login_AdminUser_InjectsRoleIntoTokenAndGrantsAdminAccess()
        {
            // Arrange — a user granted the seeded Admin role.
            var (userId, _) = await SeedAsync("adminlogin", "adminpass");
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.AssignRoleToUserAsync(context, userId, ERole.Admin);
            }

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
            await SeedAsync("plainlogin", "plainpass");

            // Act
            var (authClient, _) = await LoginAndBuildClientAsync("plainlogin", "plainpass");

            var response = await authClient.PostAsJsonAsync("/api/AdminTools/AddEditTags", Array.Empty<object>(), CancellationToken);

            // Assert — authenticated, but lacking the Admin role.
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            authClient.Dispose();
        }
    }
}
