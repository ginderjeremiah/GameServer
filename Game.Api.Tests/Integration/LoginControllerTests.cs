using Game.Abstractions.Contracts.Identity;
using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Api.Http;
using Game.Api.Models.Auth;
using Game.Api.Models.Common;
using Game.Core;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
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
        public async Task Login_ValidCredentials_ReturnsPlayerSummariesAndTokens()
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
            // Login lists the account's characters (no player is bound until SelectPlayer).
            var summary = Assert.Single(result.Data.PlayerSummaries);
            Assert.Equal(player.Id, summary.Id);
            Assert.Equal(player.Name, summary.Name);

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
        public async Task Login_BannedUser_ReturnsErrorAndEstablishesNoSession()
        {
            // A banned account with otherwise-correct credentials is rejected in the auth path: no tokens
            // are issued and no session is established.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "bannedlogin", "bannedpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            await ReloadReferenceCachesAsync();

            user.BannedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken);

            var creds = new { Username = "bannedlogin", Password = "bannedpass" };

            // Act
            var response = await Client.PostAsJsonAsync("/api/Login", creds, CancellationToken);

            // Assert — the login is rejected with a structured error and no body data.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResult>>(CancellationToken);
            Assert.NotNull(result);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(result.Data);

            // No session is established for the rejected login.
            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            Assert.Null(await sessionStore.GetSession(user.Id));
        }

        [Fact]
        public async Task SelectPlayer_OwnedCharacter_BindsSessionAndRotatesTokenIntoGameReadyState()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "selectuser", "selectpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            await ReloadReferenceCachesAsync();

            var login = await LoginAsync("selectuser", "selectpass");
            var summary = Assert.Single(login.PlayerSummaries);

            // Selecting binds the session and returns the loaded player plus a rotated, game-ready token.
            var select = await SelectPlayerAsync(login.Tokens, summary.Id);
            Assert.Equal(player.Id, select.Player.Id);
            Assert.Equal(player.Name, select.Player.Name);
            Assert.NotEqual(login.Tokens.RefreshToken, select.Tokens.RefreshToken);

            // The cached session is now established for the selected character.
            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            var session = await sessionStore.GetSession(user.Id);
            Assert.NotNull(session);
            Assert.Equal(player.Id, session.PlayerId);

            // The rotated access token authenticates Status and resolves the selected player from its claim.
            using var authClient = Factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", select.Tokens.AccessToken);
            var statusResponse = await authClient.GetAsync("/api/Login/Status", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
            var status = await statusResponse.Content.ReadFromJsonAsync<ApiResponse<Models.Player.PlayerData>>(CancellationToken);
            Assert.Equal(player.Id, status?.Data?.Id);
        }

        [Fact]
        public async Task SelectPlayer_DeliversClassLockedBaseAndSignaturePassive()
        {
            // The logged-in player payload (LoginController.BuildPlayerData) projects the character's class
            // into LockedBaseDistribution + SignaturePassive — the parity-sensitive class data the live
            // frontend battler composes its attributes from (#1126 areas D/E). The end-to-end class-delivery
            // assertions otherwise live only on CharacterCreationData, not the logged-in payload, so a
            // regression in this projection would load fine while desyncing the FE/BE anti-cheat surface.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "classdelivery", "classpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            // A distinctive locked-base fingerprint and an attribute-scaled signature passive, so every
            // projected field (incl. the scaling fields and modifier type) is pinned, not just the defaults.
            var @class = await TestDataSeeder.CreateClassWithKitAsync(
                context,
                starterSkillIds: [],
                attributeDistributions:
                [
                    (EAttribute.Strength, 12m, 2m),
                    (EAttribute.Endurance, 8m, 1m),
                ],
                passiveAttribute: EAttribute.Endurance,
                passiveAmount: 7m,
                passiveScalingAttribute: EAttribute.Strength,
                passiveScalingAmount: 0.5m,
                passiveModifierType: EModifierType.Additive);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, classId: @class.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            await ReloadReferenceCachesAsync();

            var login = await LoginAsync("classdelivery", "classpass");
            var summary = Assert.Single(login.PlayerSummaries);
            var select = await SelectPlayerAsync(login.Tokens, summary.Id);

            // The signature passive is delivered verbatim from the class.
            var passive = select.Player.SignaturePassive;
            Assert.Equal(EAttribute.Endurance, passive.AttributeId);
            Assert.Equal(7m, passive.Amount);
            Assert.Equal(EAttribute.Strength, passive.ScalingAttributeId);
            Assert.Equal(0.5m, passive.ScalingAmount);
            Assert.Equal(EModifierType.Additive, passive.ModifierType);

            // The locked-base fingerprint is delivered as the authored distribution (base + per-level), so the
            // client can rescale it on level-up. Asserted by attribute to stay independent of projection order.
            Assert.Equal(2, select.Player.LockedBaseDistribution.Count);
            var strength = Assert.Single(select.Player.LockedBaseDistribution, d => d.AttributeId == EAttribute.Strength);
            Assert.Equal(12m, strength.BaseAmount);
            Assert.Equal(2m, strength.AmountPerLevel);
            var endurance = Assert.Single(select.Player.LockedBaseDistribution, d => d.AttributeId == EAttribute.Endurance);
            Assert.Equal(8m, endurance.BaseAmount);
            Assert.Equal(1m, endurance.AmountPerLevel);
        }

        [Fact]
        public async Task SelectPlayer_CharacterOfAnotherAccount_IsRejected()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var owner = await TestDataSeeder.CreateUserAsync(context, "owneracct", "pass");
            var ownerPlayer = await TestDataSeeder.CreatePlayerAsync(context, owner.Id);
            await TestDataSeeder.CreateUserAsync(context, "attackeracct", "pass");

            var login = await LoginAsync("attackeracct", "pass");

            // The attacker selects a player they do not own — rejected before any binding.
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Login/SelectPlayer")
            {
                Content = JsonContent.Create(new { PlayerId = ownerPlayer.Id, login.Tokens.RefreshToken }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.Tokens.AccessToken);
            var response = await Client.SendAsync(request, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelectPlayerResult>>(CancellationToken);
            Assert.NotNull(result?.ErrorMessage);
            Assert.Null(result.Data);
        }

        [Fact]
        public async Task SelectPlayer_Unauthenticated_Returns401()
        {
            var response = await Client.PostAsJsonAsync("/api/Login/SelectPlayer",
                new { PlayerId = 1, RefreshToken = "irrelevant" }, CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task SwitchPlayer_Unauthenticated_Returns401()
        {
            var response = await Client.PostAsJsonAsync("/api/Login/SwitchPlayer",
                new { PlayerId = 1, RefreshToken = "irrelevant" }, CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task SwitchPlayer_TargetOfAnotherAccount_IsRejected()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var owner = await TestDataSeeder.CreateUserAsync(context, "switchowner", "pass");
            var ownerPlayer = await TestDataSeeder.CreatePlayerAsync(context, owner.Id);
            var attacker = await TestDataSeeder.CreateUserAsync(context, "switchattacker", "pass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var attackerPlayer = await TestDataSeeder.CreatePlayerAsync(context, attacker.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, attackerPlayer.Id, skill.Id);
            await ReloadReferenceCachesAsync();

            var login = await LoginAsync("switchattacker", "pass");
            var select = await SelectPlayerAsync(login.Tokens, attackerPlayer.Id);

            // Switching to a character of another account is rejected (anti-cheat) before binding it.
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Login/SwitchPlayer")
            {
                Content = JsonContent.Create(new { PlayerId = ownerPlayer.Id, select.Tokens.RefreshToken }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", select.Tokens.AccessToken);
            var response = await Client.SendAsync(request, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelectPlayerResult>>(CancellationToken);
            Assert.NotNull(result?.ErrorMessage);
            Assert.Null(result.Data);
        }

        [Fact]
        public async Task SwitchPlayer_FromOneCharacterToAnother_CreditsDepartedAndBindsTarget()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // A winning idle scenario so the departed character actually earns over its credited away window:
            // the player one-shots a fixed-power enemy in a single-zone loop.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context,
                strengthBase: 50m, strengthPerLevel: 0m, enduranceBase: 50m, endurancePerLevel: 0m);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 1m, cooldownMs: 2000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context, "switchuser", "pass");
            var departed = await TestDataSeeder.CreatePlayerAsync(context, user.Id, name: "Alpha", zoneId: zone.Id);
            var target = await TestDataSeeder.CreatePlayerAsync(context, user.Id, name: "Beta", zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, departed.Id, playerSkill.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, target.Id, playerSkill.Id);

            // Back-date the departed character's activity so the switch credits a real away window.
            var departedLevelBefore = departed.Level;
            departed.LastActivity = DateTime.UtcNow.AddMinutes(-30);
            await context.SaveChangesAsync(CancellationToken);
            await ReloadReferenceCachesAsync();

            var login = await LoginAsync("switchuser", "pass");
            var select = await SelectPlayerAsync(login.Tokens, departed.Id);

            var switched = await SwitchPlayerAsync(select.Tokens, target.Id);

            // The response binds the target character and rotates the token.
            Assert.Equal(target.Id, switched.Player.Id);
            Assert.Equal("Beta", switched.Player.Name);
            Assert.NotEqual(select.Tokens.RefreshToken, switched.Tokens.RefreshToken);

            // The cached session is now bound to the target character.
            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            var session = await sessionStore.GetSession(user.Id);
            Assert.NotNull(session);
            Assert.Equal(target.Id, session.PlayerId);

            // The departed character was credited for its away window — it gained levels from the simulated
            // victories (write-behind, so poll the persisted aggregate until the credit lands).
            await AssertPlayerLeveledAboveAsync(departed.Id, departedLevelBefore);

            // The rotated access token authenticates Status and resolves the target character from its claim.
            using var authClient = Factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", switched.Tokens.AccessToken);
            var statusResponse = await authClient.GetAsync("/api/Login/Status", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
            var status = await statusResponse.Content.ReadFromJsonAsync<ApiResponse<Models.Player.PlayerData>>(CancellationToken);
            Assert.Equal(target.Id, status?.Data?.Id);
        }

        [Fact]
        public async Task SwitchPlayer_DepartedCharacterStillHasLiveSocket_SkipsCreditButStillSwitches()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // The same winning idle setup as the credit test, so the departed character *would* level up over
            // its credited away window — making a skipped credit observable as "no level gained".
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context,
                strengthBase: 50m, strengthPerLevel: 0m, enduranceBase: 50m, endurancePerLevel: 0m);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 1m, cooldownMs: 2000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context, "switchlivesocket", "pass");
            var departed = await TestDataSeeder.CreatePlayerAsync(context, user.Id, name: "Alpha", zoneId: zone.Id);
            var target = await TestDataSeeder.CreatePlayerAsync(context, user.Id, name: "Beta", zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, departed.Id, playerSkill.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, target.Id, playerSkill.Id);

            var departedLevelBefore = departed.Level;
            departed.LastActivity = DateTime.UtcNow.AddMinutes(-30);
            await context.SaveChangesAsync(CancellationToken);
            await ReloadReferenceCachesAsync();

            var login = await LoginAsync("switchlivesocket", "pass");
            var select = await SelectPlayerAsync(login.Tokens, departed.Id);

            // The client kept the departed character's game socket open instead of tearing it down before the
            // switch — the misbehaving/malicious case the server-side guard defends against.
            await using var socketClient = new TestSocketClient();
            var wsClient = Factory.Server.CreateWebSocketClient();
            await socketClient.ConnectAsync(wsClient, user.Id, departed.Id);
            // Round-trip a command so the connection is fully registered (the presence key is set before the
            // command listener, so any response guarantees registration completed).
            await socketClient.SendCommandAsync<object>("GetStatisticTypes");

            var switched = await SwitchPlayerAsync(select.Tokens, target.Id);

            // The switch itself still proceeds — the target is bound and the token rotated.
            Assert.Equal(target.Id, switched.Player.Id);
            Assert.NotEqual(select.Tokens.RefreshToken, switched.Tokens.RefreshToken);

            // The cached session is now bound to the target character.
            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            var session = await sessionStore.GetSession(user.Id);
            Assert.NotNull(session);
            Assert.Equal(target.Id, session.PlayerId);

            // But the departed character's credit was skipped: its live battle loop owns its saves under the
            // per-socket command lock, so the off-lock HTTP credit must not run and its level does not advance.
            await AssertPlayerNotCreditedAsync(departed.Id, departedLevelBefore);
        }

        [Fact]
        public async Task Players_AuthenticatedAccount_ListsAllItsCharacters()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "switchlist", "pass");
            var first = await TestDataSeeder.CreatePlayerAsync(context, user.Id, name: "Alpha");
            var second = await TestDataSeeder.CreatePlayerAsync(context, user.Id, name: "Beta");
            await ReloadReferenceCachesAsync();

            var login = await LoginAsync("switchlist", "pass");
            var select = await SelectPlayerAsync(login.Tokens, first.Id);

            using var authClient = Factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", select.Tokens.AccessToken);
            var response = await authClient.GetAsync("/api/Login/Players", CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<PlayerSummary>>(CancellationToken);
            Assert.NotNull(result?.Data);
            Assert.Equal(new[] { first.Id, second.Id }.OrderBy(id => id), result.Data.Select(p => p.Id).OrderBy(id => id));
        }

        [Fact]
        public async Task Players_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Login/Players", CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // Switches the bound character through the real SwitchPlayer endpoint and returns the deserialized
        // result (rotated tokens plus the loaded target player).
        private async Task<SelectPlayerResult> SwitchPlayerAsync(AuthTokens tokens, int playerId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Login/SwitchPlayer")
            {
                Content = JsonContent.Create(new { PlayerId = playerId, tokens.RefreshToken }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

            var response = await Client.SendAsync(request, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelectPlayerResult>>(CancellationToken);
            Assert.NotNull(result?.Data);
            return result.Data;
        }

        // The credited save is write-behind (fire-and-forget cache write), so poll the persisted aggregate
        // until the departed character's level reflects the simulated away window.
        private async Task AssertPlayerLeveledAboveAsync(int playerId, int levelBefore)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                var persisted = await GetPersistedPlayerAsync(playerId);
                if (persisted.Level > levelBefore)
                {
                    return;
                }

                await Task.Delay(25, CancellationToken);
            }

            Assert.Fail("The departed character was not credited (its level did not advance) after the switch.");
        }

        // The inverse of AssertPlayerLeveledAboveAsync: gives any write-behind credit a window to surface and
        // asserts the persisted level never advances. The credit test shows a real credit lands well within
        // this window, so a regression (credit not skipped while a socket is live) is caught here.
        private async Task AssertPlayerNotCreditedAsync(int playerId, int levelBefore)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                var persisted = await GetPersistedPlayerAsync(playerId);
                Assert.False(persisted.Level > levelBefore,
                    "The departed character was credited despite holding a live socket; the off-lock credit should have been skipped.");
                await Task.Delay(25, CancellationToken);
            }
        }

        [Fact]
        public async Task CreateAccount_ValidCredentials_Succeeds()
        {
            // Signup creates the account only — no character, so no class is supplied (#1256).
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
            // Arrange — create the user first so the duplicate-username check rejects the second attempt.
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.CreateUserAsync(context, "duplicate", "pass");
            }

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
        public async Task CreateAccount_ConcurrentDuplicate_ReturnsCleanErrorNotServerError()
        {
            var creds = new { Username = "raceuser", Password = "racepass" };

            // Two concurrent requests racing past the existence check both reach the commit. The
            // active-username unique index lets exactly one through; the loser must surface as a clean
            // BadRequest, not the 500 the violation would otherwise raise outside the action.
            var responses = await Task.WhenAll(
                Client.PostAsJsonAsync("/api/Login/CreateAccount", creds, CancellationToken),
                Client.PostAsJsonAsync("/api/Login/CreateAccount", creds, CancellationToken));

            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest));
            Assert.DoesNotContain(responses, response => response.StatusCode == HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task CreatePlayer_AuthenticatedValidName_CreatesCharacterAndReturnsSummary()
        {
            // The class kit (its starter skills) backs the new-player blueprint's PlayerSkills.
            int classId;
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            classId = (await TestDataSeeder.CreateStandardCreatableClassAsync(context)).Id;
            var user = await TestDataSeeder.CreateUserAsync(context, "creatorctrl", "pass");
            await ReloadReferenceCachesAsync();

            var client = Factory.CreateClient();
            // A pre-selection token (no player bound) — the realistic state on the character-select screen.
            TestAuthHelper.AddAuthHeader(client, user.Id);

            var response = await client.PostAsJsonAsync("/api/Login/CreatePlayer", new { Name = "Gandalf", ClassId = classId }, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PlayerSummary>>(CancellationToken);
            Assert.NotNull(result?.Data);
            Assert.Null(result.ErrorMessage);
            Assert.Equal("Gandalf", result.Data.Name);

            // The character is persisted and attached to the account.
            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var created = await verifyContext.Players.FirstOrDefaultAsync(p => p.Id == result.Data.Id, CancellationToken);
            Assert.NotNull(created);
            Assert.Equal(user.Id, created.UserId);
            Assert.Equal(classId, created.ClassId);
            client.Dispose();
        }

        [Fact]
        public async Task CreatePlayer_Unauthenticated_Returns401()
        {
            var response = await Client.PostAsJsonAsync("/api/Login/CreatePlayer", new { Name = "Nobody" }, CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreatePlayer_InvalidName_Returns400AndCreatesNothing()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "badnamectrl", "pass");

            var client = Factory.CreateClient();
            TestAuthHelper.AddAuthHeader(client, user.Id);

            // A name past the 20-char limit is rejected with a structured error, not a 500. The name is
            // validated before the class, so the placeholder class id is never reached.
            var response = await client.PostAsJsonAsync("/api/Login/CreatePlayer",
                new { Name = "this-name-is-way-too-long-to-be-valid", ClassId = 0 }, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PlayerSummary>>(CancellationToken);
            Assert.NotNull(result?.ErrorMessage);
            Assert.Null(result.Data);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Equal(0, await verifyContext.Players.CountAsync(p => p.UserId == user.Id, CancellationToken));
            client.Dispose();
        }

        [Fact]
        public async Task CreatePlayer_AtConfiguredCap_Returns400()
        {
            // Verifies the configured default cap (6) is wired end-to-end: an account already holding the cap
            // is refused another character.
            int classId;
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            classId = (await TestDataSeeder.CreateStandardCreatableClassAsync(context)).Id;
            var user = await TestDataSeeder.CreateUserAsync(context, "cappedctrl", "pass");
            for (var i = 0; i < 6; i++)
            {
                await TestDataSeeder.CreatePlayerAsync(context, user.Id, name: $"Char{i}");
            }
            await ReloadReferenceCachesAsync();

            var client = Factory.CreateClient();
            TestAuthHelper.AddAuthHeader(client, user.Id);

            var response = await client.PostAsJsonAsync("/api/Login/CreatePlayer", new { Name = "OneTooMany", ClassId = classId }, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Equal(6, await verifyContext.Players.CountAsync(p => p.UserId == user.Id, CancellationToken));
            client.Dispose();
        }

        [Fact]
        public async Task Status_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Login/Status", CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Status_AuthenticatedButPlayerNotLoadable_Returns404WithError()
        {
            // A still-valid token whose player can't be loaded (archived/deleted between requests; here a user
            // with no player at all) must return a structured error, not a 500 — mirroring how Login surfaces a
            // missing player. Rehydration finds no player, so the session's selected id stays unresolved and the
            // player load comes back null. The missing-resource semantics surface as 404, not a blanket 400.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "playerlessstatus", "pass");

            var client = Factory.CreateClient();
            TestAuthHelper.AddAuthHeader(client, user.Id);

            var response = await client.GetAsync("/api/Login/Status", CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<Models.Player.PlayerData>>(CancellationToken);
            Assert.Null(result?.Data);
            Assert.NotNull(result?.ErrorMessage);
            client.Dispose();
        }

        [Fact]
        public async Task ActiveSession_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Login/ActiveSession", CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Status_ValidTokenWithNoSessionCache_RehydratesAndReturnsPlayer()
        {
            // A valid token with no cached session (evicted, aged out under the sliding TTL, or never
            // established on this instance) must not be reported as "not logged in" (#693). The session is
            // rehydrated from the user's player binding instead.
            var (client, user, player) = await SeedUserWithTokenButNoSessionAsync("evictedstatus");

            var response = await client.GetAsync("/api/Login/Status", CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<Models.Player.PlayerData>>(CancellationToken);
            Assert.NotNull(result?.Data);
            Assert.Null(result.ErrorMessage);
            Assert.Equal(player.Name, result.Data.Name);

            // Rehydration is in-memory only: the request resolves the player without ever writing the session
            // cache, since player-state writes belong on the socket, not this concurrent HTTP path (#937).
            await AssertSessionNotEstablishedAsync(user.Id);
            client.Dispose();
        }

        [Fact]
        public async Task ActiveSession_ValidTokenWithNoSessionCache_RehydratesAndReturnsResult()
        {
            // The pre-game active-session takeover warning is the user-visible breakage from #693: an evicted
            // session must rehydrate and report the (absent) active socket, not "not logged in".
            var (client, user, _) = await SeedUserWithTokenButNoSessionAsync("evictedactive");

            var response = await client.GetAsync("/api/Login/ActiveSession", CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ActiveSessionResult>>(CancellationToken);
            Assert.NotNull(result?.Data);
            Assert.Null(result.ErrorMessage);
            Assert.False(result.Data.Active);

            // Rehydration is in-memory only — the presence check resolves the player without writing the cache (#937).
            await AssertSessionNotEstablishedAsync(user.Id);
            client.Dispose();
        }

        [Fact]
        public async Task NonSessionEndpoint_ValidTokenWithNoSessionCache_DoesNotEstablishSession()
        {
            // The redundant per-request session read is gone (#755): an authenticated endpoint that never
            // reads player state (here DeviceInfo) must not load or rehydrate the session cache, even for a
            // user who has a resolvable player. Only the socket handshake and Status/ActiveSession do.
            var (client, user, _) = await SeedUserWithTokenButNoSessionAsync("nosessionread");
            client.DefaultRequestHeaders.TryAddWithoutValidation(ClientHints.DeviceFingerprintHeader, "fp-nosession");

            var response = await client.PostAsJsonAsync("/api/Login/DeviceInfo",
                new { DeviceMemory = 8.0, HardwareConcurrency = 4 }, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Confirm the session was never established — the cache stays empty for this user.
            await AssertSessionNotEstablishedAsync(user.Id);
            client.Dispose();
        }

        /// <summary>
        /// Seeds a user with a player (and a linked skill so the aggregate loads) and returns a client
        /// carrying a valid bearer token for that user but with no session ever established in the cache —
        /// the "valid token, evicted/absent session" state.
        /// </summary>
        private async Task<(HttpClient Client, User User, Player Player)>
            SeedUserWithTokenButNoSessionAsync(string username)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, username, "pass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            await ReloadReferenceCachesAsync();

            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            Assert.Null(await sessionStore.GetSession(user.Id));

            var client = Factory.CreateClient();
            // A post-selection token (carrying the selected-player claim) with no cached session — the
            // "valid token, evicted/absent session" state that must rehydrate from the claim.
            TestAuthHelper.AddAuthHeader(client, user.Id, player.Id);
            return (client, user, player);
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

            // Select a character so the refreshed token carries the selected player (and Status can load it).
            var login = await LoginAsync("refreshuser", "refreshpass");
            var select = await SelectPlayerAsync(login.Tokens, player.Id);

            // Act — exchange the refresh token for a new pair.
            var refreshResponse = await Client.PostAsJsonAsync("/api/Login/Refresh",
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

            var (authClient, tokens) = await LoginAndBuildClientAsync("logoutuser", "logoutpass");

            // Act
            var response = await authClient.PostAsJsonAsync("/api/Login/Logout",
                new { tokens.RefreshToken }, CancellationToken);

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
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "expiredlogout", "logoutpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            await ReloadReferenceCachesAsync();

            // Selecting a character establishes the cached session; the refresh token outlives the access token.
            var login = await LoginAsync("expiredlogout", "logoutpass");
            var select = await SelectPlayerAsync(login.Tokens, player.Id);
            await AssertSessionPresentAsync(user.Id);

            // Act — log out over the unauthenticated client (no bearer token, mimicking the expired access
            // token) carrying only the still-valid refresh token.
            var response = await Client.PostAsJsonAsync("/api/Login/Logout",
                new { select.Tokens.RefreshToken }, CancellationToken);

            // Assert — logout succeeds and the session is evicted despite the absent access token.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await AssertSessionEvictedAsync(user.Id);

            // The consumed refresh token can no longer be exchanged for new tokens.
            var refreshResponse = await Client.PostAsJsonAsync("/api/Login/Refresh",
                new { select.Tokens.RefreshToken }, CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, refreshResponse.StatusCode);
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

        // The session store write after login is fire-and-forget, so poll until the session is cached.
        private async Task AssertSessionPresentAsync(int userId)
        {
            using var scope = CreateScope();
            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (await sessionStore.GetSession(userId) is not null)
                {
                    return;
                }

                await Task.Delay(25, CancellationToken);
            }

            Assert.Fail("The session was not established in the cache after login.");
        }

        // The Clear on logout is fire-and-forget, so poll until the session disappears.
        private async Task AssertSessionEvictedAsync(int userId)
        {
            using var scope = CreateScope();
            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (await sessionStore.GetSession(userId) is null)
                {
                    return;
                }

                await Task.Delay(25, CancellationToken);
            }

            Assert.Fail("The session was not evicted from the cache on logout.");
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

        [Fact]
        public async Task CharacterCreationData_ReturnsCreatableClassesWithResolvedKitNames_ExcludingRetired()
        {
            // Arrange — a class with a kit (a starter skill + an equipped weapon) plus a retired class.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await TestDataSeeder.CreateUserAsync(context, "creationuser", "creationpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context, "Fireball");
            var weapon = await TestDataSeeder.CreateItemAsync(context, "Iron Sword", category: EItemCategory.Weapon);
            var active = await TestDataSeeder.CreateClassWithKitAsync(
                context,
                starterSkillIds: [skill.Id],
                attributeDistributions: [(EAttribute.Strength, 10m, 1m)],
                name: "Warrior",
                starterEquipment: [(weapon.Id, EEquipmentSlot.WeaponSlot)]);
            await TestDataSeeder.CreateClassWithKitAsync(context, [skill.Id], name: "Retired Class", retiredAt: DateTime.UtcNow);
            // Reload the in-memory reference caches so the freshly-seeded classes/skills/items resolve.
            await ReloadReferenceCachesAsync();

            // The endpoint is reachable pre-selection, so the login (pre-selection) token suffices.
            var login = await LoginAsync("creationuser", "creationpass");
            using var authClient = Factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Tokens.AccessToken);

            // Act
            var response = await authClient.GetAsync("/api/Login/CharacterCreationData", CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content
                .ReadFromJsonAsync<ApiEnumerableResponse<Game.Abstractions.Contracts.CreatableClass>>(CancellationToken);
            Assert.NotNull(result);
            Assert.Null(result.ErrorMessage);
            Assert.NotNull(result.Data);

            var creatable = Assert.Single(result.Data, c => c.Id == active.Id);
            Assert.Equal("Warrior", creatable.Name);
            // Starter skill/item names are resolved server-side (the screen has no reference data yet).
            var starterSkill = Assert.Single(creatable.StarterSkills);
            Assert.Equal(skill.Id, starterSkill.Id);
            Assert.Equal("Fireball", starterSkill.Name);
            var equipment = Assert.Single(creatable.StarterEquipment);
            Assert.Equal(weapon.Id, equipment.ItemId);
            Assert.Equal(EEquipmentSlot.WeaponSlot, equipment.EquipmentSlot);
            Assert.Equal("Iron Sword", equipment.Name);
            // The signature passive and attribute fingerprint are projected through verbatim.
            Assert.Equal(EAttribute.Strength, creatable.PassiveAttributeId);
            Assert.Equal(5m, creatable.PassiveAmount);
            Assert.Null(creatable.PassiveScalingAttributeId);
            Assert.Equal(0m, creatable.PassiveScalingAmount);
            Assert.Equal(EModifierType.Additive, creatable.PassiveModifierType);
            var distribution = Assert.Single(creatable.AttributeDistributions);
            Assert.Equal(EAttribute.Strength, distribution.AttributeId);
            Assert.Equal(10m, distribution.BaseAmount);
            Assert.Equal(1m, distribution.AmountPerLevel);
            // The retired class is out of circulation for new characters.
            Assert.DoesNotContain(result.Data, c => c.Name == "Retired Class");
        }

        [Fact]
        public async Task CharacterCreationData_WithoutAuthentication_IsRejected()
        {
            // No bearer token — a pre-authentication request never reaches the creatable-class payload.
            var response = await Client.GetAsync("/api/Login/CharacterCreationData", CancellationToken);
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
