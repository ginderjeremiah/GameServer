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
        public async Task Login_ValidCredentials_ReturnsTokensAndPlayer()
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
            Assert.Equal(player.Name, result.Player.Name);
            Assert.False(string.IsNullOrEmpty(result.Tokens.AccessToken));
            Assert.False(string.IsNullOrEmpty(result.Tokens.RefreshToken));
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
        public async Task Login_UserWithoutPlayer_ReturnsNoPlayer()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await TestDataSeeder.CreateUserAsync(context, "noplayer", "pass");

            var accountService = CreateAccountService(scope.ServiceProvider);

            var result = await accountService.Login("noplayer", "pass");

            Assert.False(result.Success);
            Assert.Equal(LoginStatus.NoPlayer, result.Status);
        }

        [Fact]
        public async Task ResolveSelectedPlayerId_UserWithPlayer_ReturnsFirstPlayerId()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "rehydrateuser", "pass");
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var accountService = CreateAccountService(scope.ServiceProvider);

            var playerId = await accountService.ResolveSelectedPlayerId(user.Id);

            Assert.Equal(player.Id, playerId);
        }

        [Fact]
        public async Task ResolveSelectedPlayerId_UserWithoutPlayer_ReturnsNull()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, "noplayerrehydrate", "pass");

            var accountService = CreateAccountService(scope.ServiceProvider);

            var playerId = await accountService.ResolveSelectedPlayerId(user.Id);

            Assert.Null(playerId);
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

            await accountService.Logout(login.Tokens.RefreshToken);

            // The revoked refresh token can no longer be exchanged for a new pair.
            var refreshed = await accountService.Refresh(login.Tokens.RefreshToken);
            Assert.Null(refreshed);
        }

        private static AccountService CreateAccountService(IServiceProvider provider, int iterations = 1000)
        {
            // A real PBKDF2 hasher (cheap iteration count) using the same pepper the seeder hashed with,
            // so seeded credentials verify exactly as they would in production. A higher iteration count
            // can be supplied to exercise the transparent work-factor upgrade on login.
            var hasher = new Pbkdf2PasswordHasher(Options.Create(new PasswordHashingOptions
            {
                Pepper = TestPepper,
                Iterations = iterations,
            }));

            return new AccountService(
                provider.GetRequiredService<IUsers>(),
                provider.GetRequiredService<IPlayerRepository>(),
                provider.GetRequiredService<IRefreshTokenStore>(),
                new StubAccessTokenService(),
                hasher,
                new NewPlayerFactory(),
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
            public string CreateAccessToken(int userId, IReadOnlyList<string> roles)
            {
                return $"access-token-{userId}";
            }
        }
    }
}
