using Game.Abstractions.Auth;
using Game.Abstractions.DataAccess;
using Game.Infrastructure.Entities;
using Game.Application.Auth;
using Game.Application.Services;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using NewPlayerFactory = Game.Core.Players.NewPlayerFactory;

namespace Game.Application.Tests.Services
{
    [Collection("Integration")]
    public class AccountServiceIntegrationTests : ApplicationIntegrationTestBase
    {
        private const string TestPepper = "test-pepper-value-for-integration-tests";

        public AccountServiceIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task CreateAccount_NewUsername_InsertsUserAndInitialPlayerGraph()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // The starter skills 0/1/2 must exist for the player-skill FK.
            await TestDataSeeder.CreateSkillAsync(context, "Skill0");
            await TestDataSeeder.CreateSkillAsync(context, "Skill1");
            await TestDataSeeder.CreateSkillAsync(context, "Skill2");

            var accountService = CreateAccountService(scope.ServiceProvider);

            var status = await accountService.CreateAccount("newaccount", "newpass");
            Assert.Equal(CreateAccountStatus.Success, status);

            // CreateAccount commits its own insert (so the active-username guard can be honoured), so the
            // graph is already persisted — no separate unit-of-work commit is needed.
            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var createdUser = await verifyContext.Users
                .Include(user => user.Players)
                .FirstOrDefaultAsync(user => user.Username == "newaccount", CancellationToken);

            Assert.NotNull(createdUser);
            var createdPlayer = Assert.Single(createdUser.Players);
            Assert.Equal("newaccount", createdPlayer.Name);
            Assert.Equal(1, createdPlayer.Level);
            Assert.Equal(0, createdPlayer.Exp);
            Assert.Equal(0, createdPlayer.CurrentZoneId);
            Assert.Equal(0, createdPlayer.StatPointsGained);
            Assert.Equal(0, createdPlayer.StatPointsUsed);

            // Assert the persisted values, not just the row counts: PlayerMapper.ToEntity's per-field
            // translation (skill Selected, attribute id/amount, log type/enabled) is exactly where a
            // mapper regression that preserved counts but flipped a value would otherwise slip through.
            var skills = await verifyContext.Set<PlayerSkill>()
                .Where(skill => skill.PlayerId == createdPlayer.Id)
                .ToListAsync(CancellationToken);
            Assert.Equal(new[] { 0, 1, 2 }, skills.Select(skill => skill.SkillId).OrderBy(id => id));
            Assert.All(skills, skill => Assert.True(skill.Selected));
            // Starter skills carry a sequential loadout order (0..n-1), persisted by PlayerMapper.ToEntity.
            Assert.Equal(new[] { 0, 1, 2 }, skills.OrderBy(skill => skill.SkillId).Select(skill => skill.Order));

            var attributes = await verifyContext.Set<PlayerAttribute>()
                .Where(attribute => attribute.PlayerId == createdPlayer.Id)
                .ToListAsync(CancellationToken);
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, attributes.Select(attribute => attribute.AttributeId).OrderBy(id => id));
            Assert.All(attributes, attribute => Assert.Equal(5m, attribute.Amount));

            var logPreferencesByType = await verifyContext.Set<LogPreference>()
                .Where(preference => preference.PlayerId == createdPlayer.Id)
                .ToDictionaryAsync(preference => preference.LogTypeId, preference => preference.Enabled, CancellationToken);
            Assert.Equal(7, logPreferencesByType.Count);
            Assert.False(logPreferencesByType[(int)ELogType.Damage]);
            Assert.False(logPreferencesByType[(int)ELogType.Debug]);
            Assert.True(logPreferencesByType[(int)ELogType.Exp]);
            Assert.True(logPreferencesByType[(int)ELogType.LevelUp]);
            Assert.True(logPreferencesByType[(int)ELogType.ItemFound]);
            Assert.True(logPreferencesByType[(int)ELogType.EnemyDefeated]);
            Assert.True(logPreferencesByType[(int)ELogType.SkillEffect]);
        }

        [Fact]
        public async Task CreateAccount_DuplicateUsername_ReturnsUsernameTaken()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await TestDataSeeder.CreateUserAsync(context, "existing", "pass");

            var accountService = CreateAccountService(scope.ServiceProvider);

            var status = await accountService.CreateAccount("existing", "anotherpass");

            Assert.Equal(CreateAccountStatus.UsernameTaken, status);
        }

        [Fact]
        public async Task CreateAccount_ConcurrentSameUsername_CreatesExactlyOneActiveAccount()
        {
            // The starter skills 0/1/2 must exist for each new player's player-skill FK.
            using (var seedScope = CreateScope())
            {
                var seedContext = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.CreateSkillAsync(seedContext, "Skill0");
                await TestDataSeeder.CreateSkillAsync(seedContext, "Skill1");
                await TestDataSeeder.CreateSkillAsync(seedContext, "Skill2");
            }

            // Each attempt runs on its own scope/DbContext (a context is not thread-safe). Whether both race
            // past the existence check or one observes the other's row, the active-username unique index must
            // ensure only one insert wins; the loser is reported as taken rather than failing.
            async Task<CreateAccountStatus> Attempt()
            {
                using var scope = CreateScope();
                return await CreateAccountService(scope.ServiceProvider).CreateAccount("raceuser", "racepass");
            }

            var results = await Task.WhenAll(Attempt(), Attempt());

            Assert.Equal(1, results.Count(status => status == CreateAccountStatus.Success));
            Assert.Equal(1, results.Count(status => status == CreateAccountStatus.UsernameTaken));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var activeCount = await verifyContext.Users
                .CountAsync(user => user.Username == "raceuser" && user.ArchivedAt == null, CancellationToken);
            Assert.Equal(1, activeCount);
        }

        [Fact]
        public async Task CreateAccount_UsernameOfArchivedAccount_SucceedsLeavingOneActiveRow()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // An archived row must not block reuse of its username — the partial index filters it out.
            var archived = await TestDataSeeder.CreateUserAsync(context, "reusable", "oldpass");
            archived.ArchivedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken);

            // The starter skills 0/1/2 must exist for the new player's player-skill FK.
            await TestDataSeeder.CreateSkillAsync(context, "Skill0");
            await TestDataSeeder.CreateSkillAsync(context, "Skill1");
            await TestDataSeeder.CreateSkillAsync(context, "Skill2");

            var status = await CreateAccountService(scope.ServiceProvider).CreateAccount("reusable", "newpass");
            Assert.Equal(CreateAccountStatus.Success, status);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var rows = await verifyContext.Users
                .Where(user => user.Username == "reusable")
                .ToListAsync(CancellationToken);
            Assert.Equal(2, rows.Count);
            Assert.Equal(1, rows.Count(user => user.ArchivedAt == null));
        }

        [Fact]
        public async Task ActiveUsername_SecondActiveDuplicateInsert_IsRejectedByUniqueIndex()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await TestDataSeeder.CreateUserAsync(context, "dupe", "pass");

            // A second active row for the same username must be rejected at the DB level — the guard the
            // concurrent-creation handling relies on — independent of any application-level pre-check.
            context.Users.Add(new User { Username = "dupe", PassHash = "hash", LastLogin = DateTime.UtcNow });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(CancellationToken));
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsTokensAndPlayerSummaries()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "loginuser", "loginpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            var accountService = CreateAccountService(scope.ServiceProvider);

            var result = await accountService.Login("loginuser", "loginpass");

            Assert.True(result.Success);
            Assert.Equal(user.Id, result.UserId);
            // Login lists the account's characters but binds none — that is the SelectPlayer step.
            var summary = Assert.Single(result.PlayerSummaries);
            Assert.Equal(player.Id, summary.Id);
            Assert.Equal(player.Name, summary.Name);
            Assert.False(string.IsNullOrEmpty(result.Tokens.AccessToken));
            Assert.False(string.IsNullOrEmpty(result.Tokens.RefreshToken));
        }

        [Fact]
        public async Task Login_UserWithMultiplePlayers_ReturnsAllSummaries()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "multiplayer", "pass");
            var first = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var second = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var accountService = CreateAccountService(scope.ServiceProvider);

            var result = await accountService.Login("multiplayer", "pass");

            Assert.True(result.Success);
            Assert.Equal(new[] { first.Id, second.Id }.OrderBy(id => id), result.PlayerSummaries.Select(s => s.Id).OrderBy(id => id));
        }

        [Fact]
        public async Task Login_OutdatedWorkFactor_TransparentlyRehashesToCurrentIterations()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            // CreateUserAsync seeds at the low (1000-iteration) work factor.
            var user = await TestDataSeeder.CreateUserAsync(context, "rehashuser", "rehashpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            // Logging in through a hasher with a higher work factor should upgrade the stored hash.
            var accountService = CreateAccountService(scope.ServiceProvider, iterations: 2000);
            var result = await accountService.Login("rehashuser", "rehashpass");
            Assert.True(result.Success);

            // The credential was re-derived at the new work factor in place — the cache-flush equivalent
            // is reading the row back from a fresh context.
            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var migrated = await verifyContext.Users.AsNoTracking()
                .FirstAsync(u => u.Id == user.Id, CancellationToken);
            Assert.StartsWith("$pbkdf2-sha256$2000$", migrated.PassHash);
            Assert.NotEqual(user.PassHash, migrated.PassHash);

            // Re-login against the upgraded credential still succeeds.
            using var reloginScope = CreateScope();
            var reloginResult = await CreateAccountService(reloginScope.ServiceProvider, iterations: 2000)
                .Login("rehashuser", "rehashpass");
            Assert.True(reloginResult.Success);
        }

        [Fact]
        public async Task Login_WrongPassword_ReturnsInvalidCredentials()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "wrongpass", "correctpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            var accountService = CreateAccountService(scope.ServiceProvider);

            var result = await accountService.Login("wrongpass", "incorrect");

            Assert.False(result.Success);
            Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
            Assert.Null(result.Tokens);
        }

        [Fact]
        public async Task Login_NonexistentUser_ReturnsInvalidCredentials()
        {
            using var scope = CreateScope();
            var accountService = CreateAccountService(scope.ServiceProvider);

            var result = await accountService.Login("ghost", "whatever");

            Assert.False(result.Success);
            Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
        }

        [Fact]
        public async Task Login_UserWithoutPlayer_SucceedsWithEmptySummaries()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await TestDataSeeder.CreateUserAsync(context, "noplayer", "pass");

            var accountService = CreateAccountService(scope.ServiceProvider);

            // Login no longer binds a player, so an account with no characters still authenticates — the
            // (empty) character list is what the client acts on (offering creation, handled by #1069).
            var result = await accountService.Login("noplayer", "pass");

            Assert.True(result.Success);
            Assert.Empty(result.PlayerSummaries);
            Assert.False(string.IsNullOrEmpty(result.Tokens.AccessToken));
        }

        [Fact]
        public async Task Login_BannedUser_ReturnsBanned()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            // A fully valid account (correct credentials and a loadable player) that is then banned — so the
            // rejection can only be the ban, not a missing player or bad password.
            var user = await TestDataSeeder.CreateUserAsync(context, "banneduser", "bannedpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            await ReloadReferenceCachesAsync();

            user.BannedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken);

            var accountService = CreateAccountService(scope.ServiceProvider);

            var result = await accountService.Login("banneduser", "bannedpass");

            Assert.False(result.Success);
            Assert.Equal(LoginStatus.Banned, result.Status);
            Assert.Null(result.Tokens);
        }

        [Fact]
        public async Task Login_BannedUserWithWrongPassword_ReturnsInvalidCredentials()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "bannedwrongpass", "correctpass");
            user.BannedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken);

            var accountService = CreateAccountService(scope.ServiceProvider);

            // The ban is disclosed only after the credentials verify, so a wrong password yields the generic
            // invalid-credentials result rather than leaking that the account is banned.
            var result = await accountService.Login("bannedwrongpass", "incorrect");

            Assert.False(result.Success);
            Assert.Equal(LoginStatus.InvalidCredentials, result.Status);
            Assert.Null(result.Tokens);
        }

        [Fact]
        public async Task SelectPlayer_OwnedPlayer_RotatesTokensAndReturnsPlayer()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "selectuser", "pass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            await ReloadReferenceCachesAsync();

            var accountService = CreateAccountService(scope.ServiceProvider);
            var login = await accountService.Login("selectuser", "pass");
            Assert.True(login.Success);

            var result = await accountService.SelectPlayer(user.Id, player.Id, login.Tokens.RefreshToken);

            Assert.True(result.Success);
            Assert.Equal(player.Id, result.Player.Id);
            Assert.Equal(player.Name, result.Player.Name);
            Assert.False(string.IsNullOrEmpty(result.Tokens.AccessToken));
            Assert.NotEqual(login.Tokens.RefreshToken, result.Tokens.RefreshToken);

            // The login refresh token was consumed by the selection, so it can no longer be refreshed.
            Assert.Null(await accountService.Refresh(login.Tokens.RefreshToken));
            // The rotated refresh token re-issues a pair that keeps the selected player bound.
            Assert.NotNull(await accountService.Refresh(result.Tokens.RefreshToken));
        }

        [Fact]
        public async Task SelectPlayer_PlayerOfAnotherAccount_IsRejectedAndLeavesTokenIntact()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var owner = await TestDataSeeder.CreateUserAsync(context, "owner", "pass");
            var attacker = await TestDataSeeder.CreateUserAsync(context, "attacker", "pass");
            var ownerPlayer = await TestDataSeeder.CreatePlayerAsync(context, owner.Id);

            var accountService = CreateAccountService(scope.ServiceProvider);
            var login = await accountService.Login("attacker", "pass");
            Assert.True(login.Success);

            // Selecting a player the caller does not own is an anti-cheat rejection.
            var result = await accountService.SelectPlayer(attacker.Id, ownerPlayer.Id, login.Tokens.RefreshToken);

            Assert.False(result.Success);
            Assert.Equal(SelectPlayerStatus.NotOwned, result.Status);
            // The rejection happens before the refresh token is consumed, so the caller can still proceed.
            Assert.NotNull(await accountService.Refresh(login.Tokens.RefreshToken));
        }

        [Fact]
        public async Task SelectPlayer_InvalidRefreshToken_IsRejected()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "badtoken", "pass");
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var accountService = CreateAccountService(scope.ServiceProvider);

            var result = await accountService.SelectPlayer(user.Id, player.Id, "not-a-real-token");

            Assert.False(result.Success);
            Assert.Equal(SelectPlayerStatus.InvalidToken, result.Status);
        }

        [Fact]
        public async Task GetPlayers_ReturnsAllOfTheAccountsCharacters()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "switcher", "pass");
            var first = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var second = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var accountService = CreateAccountService(scope.ServiceProvider);

            var players = await accountService.GetPlayers(user.Id);

            Assert.Equal(new[] { first.Id, second.Id }.OrderBy(id => id), players.Select(p => p.Id).OrderBy(id => id));
        }

        [Fact]
        public async Task GetPlayers_OtherAccountsPlayers_AreNotReturned()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "owner", "pass");
            var mine = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            var other = await TestDataSeeder.CreateUserAsync(context, "stranger", "pass");
            await TestDataSeeder.CreatePlayerAsync(context, other.Id);

            var accountService = CreateAccountService(scope.ServiceProvider);

            var players = await accountService.GetPlayers(user.Id);

            Assert.Equal(new[] { mine.Id }, players.Select(p => p.Id));
        }

        [Fact]
        public async Task CreatePlayer_ValidName_CreatesCharacterAttachedToAccount()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            // The starter skills 0/1/2 must exist for the new player's player-skill FK.
            await TestDataSeeder.CreateSkillAsync(context, "Skill0");
            await TestDataSeeder.CreateSkillAsync(context, "Skill1");
            await TestDataSeeder.CreateSkillAsync(context, "Skill2");
            var user = await TestDataSeeder.CreateUserAsync(context, "creator", "pass");

            var accountService = CreateAccountService(scope.ServiceProvider);

            // A surrounding-whitespace name is normalized (trimmed) before persistence.
            var result = await accountService.CreatePlayer(user.Id, "  Aragorn  ");

            Assert.True(result.Success);
            Assert.Equal("Aragorn", result.Player.Name);
            Assert.Equal(NewPlayerFactory.StartingZoneId, result.Player.CurrentZoneId);

            // The character is attached to the account and built from the new-player blueprint (starter skills).
            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var created = await verifyContext.Players
                .FirstOrDefaultAsync(p => p.Id == result.Player.Id, CancellationToken);
            Assert.NotNull(created);
            Assert.Equal(user.Id, created.UserId);
            Assert.Equal("Aragorn", created.Name);
            Assert.Equal(1, created.Level);

            var skills = await verifyContext.Set<PlayerSkill>()
                .Where(skill => skill.PlayerId == created.Id)
                .Select(skill => skill.SkillId)
                .OrderBy(id => id)
                .ToListAsync(CancellationToken);
            Assert.Equal(new[] { 0, 1, 2 }, skills);
        }

        [Fact]
        public async Task CreatePlayer_AdditionalCharacter_DoesNotDisturbExistingOnes()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await TestDataSeeder.CreateSkillAsync(context, "Skill0");
            await TestDataSeeder.CreateSkillAsync(context, "Skill1");
            await TestDataSeeder.CreateSkillAsync(context, "Skill2");
            var user = await TestDataSeeder.CreateUserAsync(context, "secondchar", "pass");
            var existing = await TestDataSeeder.CreatePlayerAsync(context, user.Id, name: "First");

            var accountService = CreateAccountService(scope.ServiceProvider);

            var result = await accountService.CreatePlayer(user.Id, "Second");

            Assert.True(result.Success);
            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            var names = await verifyContext.Players
                .Where(p => p.UserId == user.Id)
                .Select(p => p.Name)
                .OrderBy(name => name)
                .ToListAsync(CancellationToken);
            Assert.Equal(new[] { "First", "Second" }, names);
            Assert.NotEqual(existing.Id, result.Player.Id);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("waaaaaaaaaaaaaaaaaaaaytoolong")]
        public async Task CreatePlayer_InvalidName_IsRejectedAndCreatesNothing(string name)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "badnamer", "pass");

            var accountService = CreateAccountService(scope.ServiceProvider);

            var result = await accountService.CreatePlayer(user.Id, name);

            Assert.False(result.Success);
            Assert.Equal(CreatePlayerStatus.InvalidName, result.Status);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Equal(0, await verifyContext.Players.CountAsync(p => p.UserId == user.Id, CancellationToken));
        }

        [Fact]
        public async Task CreatePlayer_AtCap_IsRejected()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "capped", "pass");
            await TestDataSeeder.CreatePlayerAsync(context, user.Id, name: "Only");

            // A cap of 1 with one existing character means the next creation is over the cap.
            var accountService = CreateAccountService(scope.ServiceProvider, maxPlayersPerAccount: 1);

            var result = await accountService.CreatePlayer(user.Id, "TooMany");

            Assert.False(result.Success);
            Assert.Equal(CreatePlayerStatus.CapReached, result.Status);

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Equal(1, await verifyContext.Players.CountAsync(p => p.UserId == user.Id, CancellationToken));
        }

        [Fact]
        public async Task CreatePlayer_ConcurrentAtCapBoundary_NeverExceedsTheCap()
        {
            using (var seedScope = CreateScope())
            {
                var seedContext = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.CreateSkillAsync(seedContext, "Skill0");
                await TestDataSeeder.CreateSkillAsync(seedContext, "Skill1");
                await TestDataSeeder.CreateSkillAsync(seedContext, "Skill2");
            }

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "racechars", "pass");
            await TestDataSeeder.CreatePlayerAsync(context, user.Id, name: "Existing");

            // Two concurrent creations race the cap of 2 (one slot left). The data tier serializes the count
            // check with the insert under the user-row lock, so exactly one wins and the cap is never exceeded.
            async Task<AccountCreatePlayerResult> Attempt(string name)
            {
                using var attemptScope = CreateScope();
                return await CreateAccountService(attemptScope.ServiceProvider, maxPlayersPerAccount: 2).CreatePlayer(user.Id, name);
            }

            var results = await Task.WhenAll(Attempt("RaceA"), Attempt("RaceB"));

            Assert.Equal(1, results.Count(r => r.Success));
            Assert.Equal(1, results.Count(r => r.Status == CreatePlayerStatus.CapReached));

            using var verifyScope = CreateScope();
            var verifyContext = verifyScope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Equal(2, await verifyContext.Players.CountAsync(p => p.UserId == user.Id, CancellationToken));
        }

        [Fact]
        public async Task Refresh_ValidToken_RotatesToNewPair()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "refreshuser", "refreshpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            var accountService = CreateAccountService(scope.ServiceProvider);
            var login = await accountService.Login("refreshuser", "refreshpass");
            Assert.True(login.Success);

            var refreshed = await accountService.Refresh(login.Tokens.RefreshToken);

            Assert.NotNull(refreshed);
            Assert.False(string.IsNullOrEmpty(refreshed.AccessToken));
            Assert.NotEqual(login.Tokens.RefreshToken, refreshed.RefreshToken);

            // The original refresh token is single-use, so replaying it now fails.
            var replayed = await accountService.Refresh(login.Tokens.RefreshToken);
            Assert.Null(replayed);
        }

        [Fact]
        public async Task Refresh_InvalidToken_ReturnsNull()
        {
            using var scope = CreateScope();
            var accountService = CreateAccountService(scope.ServiceProvider);

            var refreshed = await accountService.Refresh("not-a-real-token");

            Assert.Null(refreshed);
        }

        [Fact]
        public async Task Logout_RevokesRefreshToken()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "logoutuser", "logoutpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();

            var accountService = CreateAccountService(scope.ServiceProvider);
            var login = await accountService.Login("logoutuser", "logoutpass");
            Assert.True(login.Success);

            // Logout resolves the owning user from the consumed token so the caller can evict that session.
            var loggedOutUserId = await accountService.Logout(login.Tokens.RefreshToken);
            Assert.Equal(user.Id, loggedOutUserId);

            // The revoked refresh token can no longer be exchanged for a new pair.
            var refreshed = await accountService.Refresh(login.Tokens.RefreshToken);
            Assert.Null(refreshed);
        }

        [Fact]
        public async Task Logout_UnknownToken_ReturnsNull()
        {
            // An unknown/expired/already-consumed token resolves to no user — a safe no-op so the caller
            // evicts nothing rather than throwing.
            using var scope = CreateScope();
            var accountService = CreateAccountService(scope.ServiceProvider);

            var loggedOutUserId = await accountService.Logout("not-a-real-token");

            Assert.Null(loggedOutUserId);
        }

        [Fact]
        public async Task Login_ConsecutiveFailuresPastThreshold_BacksOffWithRetryAfter()
        {
            using var scope = CreateScope();
            var options = new LoginBackoffOptions { FailureThreshold = 2, BaseDelaySeconds = 1, MaxDelaySeconds = 4, FailureWindowSeconds = 60 };
            var clock = new MutableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var accountService = CreateAccountService(scope.ServiceProvider, backoffOptions: options, timeProvider: clock);

            // The first threshold+1 failures run the credential check (rejected as invalid, not backed off):
            // the (threshold+1)th failure is the one that first arms the lock.
            for (var i = 0; i < options.FailureThreshold + 1; i++)
            {
                var attempt = await accountService.Login("backoffghost", "wrong");
                Assert.Equal(LoginStatus.InvalidCredentials, attempt.Status);
            }

            // The next attempt is now within the backoff window and is rejected before the credential check.
            var backedOff = await accountService.Login("backoffghost", "wrong");

            Assert.Equal(LoginStatus.TooManyAttempts, backedOff.Status);
            Assert.NotNull(backedOff.RetryAfter);
            Assert.True(backedOff.RetryAfter > TimeSpan.Zero);
        }

        [Fact]
        public async Task Login_AfterBackoffWindowElapses_IsAllowedAgain()
        {
            using var scope = CreateScope();
            var options = new LoginBackoffOptions { FailureThreshold = 2, BaseDelaySeconds = 1, MaxDelaySeconds = 4, FailureWindowSeconds = 60 };
            var clock = new MutableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var accountService = CreateAccountService(scope.ServiceProvider, backoffOptions: options, timeProvider: clock);

            // Drive the account into an active backoff window.
            for (var i = 0; i < options.FailureThreshold + 1; i++)
            {
                await accountService.Login("backoffelapse", "wrong");
            }
            var backedOff = await accountService.Login("backoffelapse", "wrong");
            Assert.Equal(LoginStatus.TooManyAttempts, backedOff.Status);

            // Past the cap the lock has expired, so the owner is allowed to try again — proving this is a
            // bounded slowdown, not a hard lockout an attacker who knows the username could hold open forever.
            clock.Advance(TimeSpan.FromSeconds(options.MaxDelaySeconds + 1));
            var afterWindow = await accountService.Login("backoffelapse", "wrong");

            Assert.Equal(LoginStatus.InvalidCredentials, afterWindow.Status);
        }

        [Fact]
        public async Task Login_SuccessfulLogin_ResetsFailureStreak()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "backoffreset", "correctpass");
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            await ReloadReferenceCachesAsync();

            var options = new LoginBackoffOptions { FailureThreshold = 2, BaseDelaySeconds = 1, MaxDelaySeconds = 4, FailureWindowSeconds = 60 };
            var clock = new MutableTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var accountService = CreateAccountService(scope.ServiceProvider, backoffOptions: options, timeProvider: clock);

            // Accrue failures up to the threshold (no lock yet), then a correct login clears the streak.
            for (var i = 0; i < options.FailureThreshold; i++)
            {
                Assert.Equal(LoginStatus.InvalidCredentials, (await accountService.Login("backoffreset", "wrong")).Status);
            }
            Assert.True((await accountService.Login("backoffreset", "correctpass")).Success);

            // The streak restarted from zero, so the same number of fresh failures stays under the threshold
            // and is not backed off — had the prior failures persisted, the second of these would be locked.
            for (var i = 0; i < options.FailureThreshold; i++)
            {
                Assert.Equal(LoginStatus.InvalidCredentials, (await accountService.Login("backoffreset", "wrong")).Status);
            }
        }

        /// <summary>A test clock whose "now" only advances when explicitly told to, so the time-based backoff
        /// window can be driven deterministically without real waits.</summary>
        private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
        {
            private DateTimeOffset _now = start;

            public override DateTimeOffset GetUtcNow() => _now;

            public void Advance(TimeSpan by) => _now += by;
        }

        private static AccountService CreateAccountService(
            IServiceProvider provider,
            int iterations = 1000,
            LoginBackoffOptions? backoffOptions = null,
            TimeProvider? timeProvider = null,
            int maxPlayersPerAccount = 6)
        {
            // A real PBKDF2 hasher (cheap iteration count) using the same pepper the seeder hashed with,
            // so seeded credentials verify exactly as they would in production. A higher iteration count
            // can be supplied to exercise the transparent work-factor upgrade on login.
            var hasher = new Pbkdf2PasswordHasher(Options.Create(new PasswordHashingOptions
            {
                Pepper = TestPepper,
                Iterations = iterations,
            }));

            // The backoff guard runs over the real Redis-backed store; a tight options set plus a controllable
            // clock can be supplied to exercise the backoff deterministically. The default options' threshold
            // (5) keeps the single-attempt login tests above unaffected.
            var backoffGuard = new LoginBackoffGuard(
                provider.GetRequiredService<ILoginBackoffStore>(),
                new LoginBackoffPolicy(Options.Create(backoffOptions ?? new LoginBackoffOptions())),
                timeProvider ?? TimeProvider.System);

            return new AccountService(
                provider.GetRequiredService<IUsers>(),
                provider.GetRequiredService<IPlayerRepository>(),
                provider.GetRequiredService<IRefreshTokenStore>(),
                new StubAccessTokenService(),
                hasher,
                backoffGuard,
                new NewPlayerFactory(),
                Options.Create(new PlayerCreationOptions { MaxPlayersPerAccount = maxPlayersPerAccount }),
                NullLogger<AccountService>.Instance);
        }

        /// <summary>
        /// A trivial access-token service for the application-layer tests. The real JWT signing/format is
        /// a presentation-edge concern verified end-to-end by the API integration tests
        /// (<c>LoginControllerTests</c>); these tests exercise the account orchestration against the real
        /// Postgres/Redis collaborators, for which the access-token contents are irrelevant.
        /// </summary>
        private sealed class StubAccessTokenService : IAccessTokenService
        {
            public string CreateAccessToken(int userId, IReadOnlyList<string> roles, int? playerId = null)
            {
                return playerId is int selected ? $"access-token-{userId}-{selected}" : $"access-token-{userId}";
            }
        }
    }
}
