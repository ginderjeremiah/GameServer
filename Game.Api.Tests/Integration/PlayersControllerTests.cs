using Game.Abstractions.Contracts.Identity;
using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Api;
using Game.Api.Models.Auth;
using Game.Api.Models.Common;
using Game.Api.Services;
using Game.Application.Services;
using Game.Core;
using Game.Core.Battle.Offline;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class PlayersControllerTests : ApiIntegrationTestBase
    {
        public PlayersControllerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task SelectPlayer_OwnedCharacter_BindsSessionAndRotatesTokenIntoGameReadyState()
        {
            var (userId, playerId) = await SeedAsync("selectuser", "selectpass");

            var login = await LoginAsync("selectuser", "selectpass");
            var summary = Assert.Single(login.PlayerSummaries);

            // Selecting binds the session and returns the loaded player plus a rotated, game-ready token.
            var select = await SelectPlayerAsync(login.Tokens, summary.Id);
            Assert.Equal(playerId, select.Player.Id);
            Assert.Equal("TestPlayer", select.Player.Name);
            Assert.NotEqual(login.Tokens.RefreshToken, select.Tokens.RefreshToken);

            // The cached session is now established for the selected character.
            using var scope = CreateScope();
            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            var session = await sessionStore.GetSession(userId);
            Assert.NotNull(session);
            Assert.Equal(playerId, session.PlayerId);

            // The rotated access token authenticates Status and resolves the selected player from its claim.
            using var authClient = Factory.CreateClient();
            authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", select.Tokens.AccessToken);
            var statusResponse = await authClient.GetAsync("/api/Auth/Status", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
            var status = await statusResponse.Content.ReadFromJsonAsync<ApiResponse<Models.Player.PlayerData>>(CancellationToken);
            Assert.Equal(playerId, status?.Data?.Id);
        }

        [Fact]
        public async Task SelectPlayer_ReEntersSameCharacterWithCachedInFlightBattle_PreservesTheBattle()
        {
            // A credential re-login (rather than a plain socket reconnect) must not discard the same
            // character's cache-only in-flight battle snapshot (#1818) — it exists nowhere else, and the
            // battle already resumes fine across a plain reconnect, so behavior shouldn't hinge on how the
            // client re-entered.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "relogin", "reloginpass");
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await ReloadReferenceCachesAsync();

            var firstLogin = await LoginAsync("relogin", "reloginpass");
            await SelectPlayerAsync(firstLogin.Tokens, player.Id);

            // Simulate quitting mid-battle: the session cache holds an in-flight battle snapshot that exists
            // nowhere else (the write-behind path is what would normally populate this via the game socket).
            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            var inFlight = new Game.Core.Players.PlayerState { PlayerId = player.Id };
            inFlight.SetActiveBattle(
                enemyId: 1, level: 1, enemySkillIds: [1], seed: 1,
                startTime: DateTime.UtcNow, snapshot: new Game.Core.Battle.BattleSnapshot { Level = 1, StatAllocations = [], EquippedItems = [], SkillIds = [] },
                zoneId: 1, isBossBattle: false);
            sessionStore.Update(inFlight, user.Id);

            // Log back in with credentials (a new pre-selection token, not a plain socket reconnect) and
            // re-select the same character.
            var secondLogin = await LoginAsync("relogin", "reloginpass");
            await SelectPlayerAsync(secondLogin.Tokens, player.Id);

            var session = await sessionStore.GetSession(user.Id);
            Assert.NotNull(session);
            Assert.True(session.HasActiveBattle);
            Assert.Equal(inFlight.ActiveEnemyId, session.ActiveEnemyId);
        }

        [Fact]
        public async Task SelectPlayer_DeliversClassLockedBaseAndSignaturePassive()
        {
            // The logged-in player payload (PlayerDataAssembler.Build) projects the character's class into
            // LockedBaseDistribution + SignaturePassive — the parity-sensitive class data the live frontend
            // battler composes its attributes from (#1126 areas D/E). The end-to-end class-delivery assertions
            // otherwise live only on CharacterCreationData, not the logged-in payload, so a regression in this
            // projection would load fine while desyncing the FE/BE anti-cheat surface.
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
        public async Task SelectPlayer_DeliversTheLivePlayerRating()
        {
            // The logged-in player payload carries the player's combat-rating capability measure (spike #1526
            // Decision 7) — a numeric companion to the attributes screen, recomputed fresh from current state.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "ratingdelivery", "ratingpass");
            await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await ReloadReferenceCachesAsync();

            var login = await LoginAsync("ratingdelivery", "ratingpass");
            var summary = Assert.Single(login.PlayerSummaries);
            var select = await SelectPlayerAsync(login.Tokens, summary.Id);

            Assert.True(select.Player.PlayerRating > 0);
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
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Players/SelectPlayer")
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
            var response = await Client.PostAsJsonAsync("/api/Players/SelectPlayer",
                new { PlayerId = 1, RefreshToken = "irrelevant" }, CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task SwitchPlayer_Unauthenticated_Returns401()
        {
            var response = await Client.PostAsJsonAsync("/api/Players/SwitchPlayer",
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
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Players/SwitchPlayer")
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
            // the player one-shots a fixed-power enemy in a single-zone loop. The enemy's authored skill deals
            // the same raw DPS as the player's (4000 dmg / 2000ms = 1000 dmg / 500ms) so their combat ratings
            // come out roughly matched (a non-trivial exp payout); its 2000ms cooldown is far longer than the
            // ~500ms fight, so it never actually fires and the one-shot-kill determinism is unaffected.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context,
                strengthBase: 50m, strengthPerLevel: 0m, enduranceBase: 50m, endurancePerLevel: 0m);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 4000m, cooldownMs: 2000);
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
            var statusResponse = await authClient.GetAsync("/api/Auth/Status", CancellationToken);
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
        public async Task SwitchPlayer_CreditsDepartedCharacter_ReleasesTheSwitchCreditClaimAfterward()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // The same winning idle setup the credit test uses, so SimulateSwitchProgress runs its full path
            // (resolving a viable zone/enemy) rather than an edge case unrelated to what this test checks.
            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context,
                strengthBase: 50m, strengthPerLevel: 0m, enduranceBase: 50m, endurancePerLevel: 0m);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 4000m, cooldownMs: 2000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context, "switchclaimrelease", "pass");
            var departed = await TestDataSeeder.CreatePlayerAsync(context, user.Id, name: "Alpha", zoneId: zone.Id);
            var target = await TestDataSeeder.CreatePlayerAsync(context, user.Id, name: "Beta", zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, departed.Id, playerSkill.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, target.Id, playerSkill.Id);
            departed.LastActivity = DateTime.UtcNow.AddMinutes(-30);
            await context.SaveChangesAsync(CancellationToken);
            await ReloadReferenceCachesAsync();

            var login = await LoginAsync("switchclaimrelease", "pass");
            var select = await SelectPlayerAsync(login.Tokens, departed.Id);
            await SwitchPlayerAsync(select.Tokens, target.Id);

            // The atomic claim SwitchPlayer takes on the departed character's presence key (#2041) must not
            // linger after the credit completes — a stuck claim would defer every later reconnect for that
            // character behind it until the claim's TTL finally expired.
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            var presenceKey = $"{Constants.CACHE_PLAYER_SOCKET_PREFIX}_{departed.Id}";
            Assert.Null(await cache.Get(presenceKey, CancellationToken));
        }

        [Fact]
        public async Task SwitchPlayer_CreditFaults_StillReleasesTheSwitchCreditClaim()
        {
            // #2094 ride-along: CreditDepartedCharacter's finally releases the switch-credit claim on both the
            // success path (pinned above) and a faulted/cancelled SimulateSwitchProgress, but only the success
            // path was previously covered. A stuck claim from a faulted credit would defer every later reconnect
            // for the departed character behind it until the claim's own TTL finally expired.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var playerSkill = await TestDataSeeder.CreateSkillAsync(context, "Smash", baseDamage: 1000m, cooldownMs: 500);
            var enemy = await TestDataSeeder.CreateEnemyAsync(context,
                strengthBase: 50m, strengthPerLevel: 0m, enduranceBase: 50m, endurancePerLevel: 0m);
            var enemySkill = await TestDataSeeder.CreateSkillAsync(context, "Poke", baseDamage: 4000m, cooldownMs: 2000);
            await TestDataSeeder.LinkSkillToEnemyAsync(context, enemy.Id, enemySkill.Id);
            var zone = await TestDataSeeder.CreateZoneAsync(context, levelMin: 1, levelMax: 1);
            await TestDataSeeder.LinkEnemyToZoneAsync(context, zone.Id, enemy.Id);

            var user = await TestDataSeeder.CreateUserAsync(context, "switchcreditfault", "pass");
            var departed = await TestDataSeeder.CreatePlayerAsync(context, user.Id, name: "Alpha", zoneId: zone.Id);
            var target = await TestDataSeeder.CreatePlayerAsync(context, user.Id, name: "Beta", zoneId: zone.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, departed.Id, playerSkill.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, target.Id, playerSkill.Id);
            departed.LastActivity = DateTime.UtcNow.AddMinutes(-30);
            await context.SaveChangesAsync(CancellationToken);
            await ReloadReferenceCachesAsync();

            // A CharacterSelectionService built by hand, swapping in an OfflineProgressService whose progress
            // save always faults — a targeted stand-in for a genuine SimulateSwitchProgress failure (a
            // transient Redis/DB blip) that leaves the claim-release finally block as the only thing standing
            // between this and a stuck claim.
            var faultingOfflineService = new OfflineProgressService(
                scope.ServiceProvider.GetRequiredService<IPlayerRepository>(),
                new SaveThrowsPlayerProgressRepository(scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>()),
                scope.ServiceProvider.GetRequiredService<IItems>(),
                scope.ServiceProvider.GetRequiredService<IItemMods>(),
                scope.ServiceProvider.GetRequiredService<ISkills>(),
                scope.ServiceProvider.GetRequiredService<IProficiencies>(),
                scope.ServiceProvider.GetRequiredService<IClasses>(),
                scope.ServiceProvider.GetRequiredService<IEnemies>(),
                scope.ServiceProvider.GetRequiredService<OfflineProgressSimulator>(),
                scope.ServiceProvider.GetRequiredService<ChallengeRewardService>(),
                scope.ServiceProvider.GetRequiredService<ProficiencyRewardService>(),
                scope.ServiceProvider.GetRequiredService<ZoneResolutionService>(),
                scope.ServiceProvider.GetRequiredService<BattleService>());

            var faultingSessionService = new SessionService(scope.ServiceProvider.GetRequiredService<ISessionStore>());
            faultingSessionService.SetAuthenticatedUser(user.Id, departed.Id);

            var playerDataAssembler = new PlayerDataAssembler(
                scope.ServiceProvider.GetRequiredService<IClasses>(),
                scope.ServiceProvider.GetRequiredService<BattleService>());

            var characterSelectionService = new CharacterSelectionService(
                faultingSessionService,
                scope.ServiceProvider.GetRequiredService<SessionInitializer>(),
                scope.ServiceProvider.GetRequiredService<AccountService>(),
                scope.ServiceProvider.GetRequiredService<SocketManagerService>(),
                scope.ServiceProvider.GetRequiredService<PlayerService>(),
                faultingOfflineService,
                playerDataAssembler,
                scope.ServiceProvider.GetRequiredService<ILogger<CharacterSelectionService>>());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => characterSelectionService.SwitchPlayer(user.Id, target.Id, "unused", CancellationToken));

            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
            var presenceKey = $"{Constants.CACHE_PLAYER_SOCKET_PREFIX}_{departed.Id}";
            Assert.Null(await cache.Get(presenceKey, CancellationToken));
        }

        /// <summary>Decorates a real <see cref="IPlayerProgressRepository"/>, delegating every read but always
        /// faulting on <see cref="Save"/> — stands in for a transient persistence blip mid-credit without
        /// reaching into the write-behind internals a genuine one would exercise. Domain types are qualified
        /// (rather than a <c>using Game.Core.Players</c>/<c>Game.Core.Progress</c>) since this file's
        /// <see cref="Game.Infrastructure.Entities"/> import already claims <c>Player</c>/<c>PlayerChallenge</c>/
        /// <c>PlayerStatistic</c>/<c>PlayerProficiency</c> for the EF entities the rest of the suite seeds with.</summary>
        private sealed class SaveThrowsPlayerProgressRepository(IPlayerProgressRepository inner) : IPlayerProgressRepository
        {
            public Task<Game.Core.Progress.PlayerProgress> Load(Game.Core.Players.Player player, CancellationToken cancellationToken = default) => inner.Load(player, cancellationToken);
            public Task Save(Game.Core.Progress.PlayerProgress progress, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Simulated progress-save fault.");
            public Task<List<Game.Core.Progress.PlayerStatistic>> GetStatistics(int playerId, CancellationToken cancellationToken = default) => inner.GetStatistics(playerId, cancellationToken);
            public Task<List<Game.Core.Progress.PlayerChallenge>> GetChallenges(int playerId, CancellationToken cancellationToken = default) => inner.GetChallenges(playerId, cancellationToken);
            public Task<List<Game.Core.Progress.PlayerProficiency>> GetProficiencies(int playerId, CancellationToken cancellationToken = default) => inner.GetProficiencies(playerId, cancellationToken);
            public Task<HashSet<int>> GetCompletedChallengeIds(int playerId, CancellationToken cancellationToken = default) => inner.GetCompletedChallengeIds(playerId, cancellationToken);
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
            var response = await authClient.GetAsync("/api/Players", CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<PlayerSummary>>(CancellationToken);
            Assert.NotNull(result?.Data);
            Assert.Equal(new[] { first.Id, second.Id }.OrderBy(id => id), result.Data.Select(p => p.Id).OrderBy(id => id));
        }

        [Fact]
        public async Task Players_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/Players", CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // Switches the bound character through the real SwitchPlayer endpoint and returns the deserialized
        // result (rotated tokens plus the loaded target player).
        private async Task<SelectPlayerResult> SwitchPlayerAsync(AuthTokens tokens, int playerId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Players/SwitchPlayer")
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
            var persisted = await PollingHelper.PollUntilAsync(
                () => GetPersistedPlayerAsync(playerId), p => p.Level > levelBefore, timeoutMs: 5000);

            Assert.True(persisted.Level > levelBefore,
                "The departed character was not credited (its level did not advance) after the switch.");
        }

        // The inverse of AssertPlayerLeveledAboveAsync: gives any write-behind credit a window to surface and
        // asserts the persisted level never advances. The credit test shows a real credit lands well within
        // this window, so a regression (credit not skipped while a socket is live) is caught here.
        private async Task AssertPlayerNotCreditedAsync(int playerId, int levelBefore)
        {
            var persisted = await PollingHelper.PollUntilAsync(
                () => GetPersistedPlayerAsync(playerId), p => p.Level > levelBefore, timeoutMs: 2000);

            Assert.False(persisted.Level > levelBefore,
                "The departed character was credited despite holding a live socket; the off-lock credit should have been skipped.");
        }

        [Fact]
        public async Task CreatePlayer_AuthenticatedValidName_CreatesCharacterAndReturnsSummary()
        {
            // The class kit (its starter skills) backs the new-player blueprint's PlayerSkills.
            int classId;
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // NewPlayerFactory.StartingZoneId is 0 — seed a zone so it lands there and the real creation path
            // can resolve the new player's CurrentZoneId FK.
            await TestDataSeeder.CreateZoneAsync(context);

            classId = (await TestDataSeeder.CreateStandardCreatableClassAsync(context)).Id;
            var user = await TestDataSeeder.CreateUserAsync(context, "creatorctrl", "pass");
            await ReloadReferenceCachesAsync();

            var client = Factory.CreateClient();
            // A pre-selection token (no player bound) — the realistic state on the character-select screen.
            TestAuthHelper.AddAuthHeader(client, user.Id);

            var response = await client.PostAsJsonAsync("/api/Players/CreatePlayer", new { Name = "Gandalf", ClassId = classId }, CancellationToken);

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
            var response = await Client.PostAsJsonAsync("/api/Players/CreatePlayer", new { Name = "Nobody" }, CancellationToken);

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
            var response = await client.PostAsJsonAsync("/api/Players/CreatePlayer",
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

            var response = await client.PostAsJsonAsync("/api/Players/CreatePlayer", new { Name = "OneTooMany", ClassId = classId }, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Equal(6, await verifyContext.Players.CountAsync(p => p.UserId == user.Id, CancellationToken));
            client.Dispose();
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
            var response = await authClient.GetAsync("/api/Players/CharacterCreationData", CancellationToken);

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
            var response = await Client.GetAsync("/api/Players/CharacterCreationData", CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
