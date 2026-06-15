using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Game.Application;
using Game.Core;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Verifies the per-command cancellation budget now reaches the actual data-tier I/O (#558): once a token is
    /// threaded through the repositories, the cache/pub-sub services, and the unit of work, an already-cancelled
    /// token unwinds the operation promptly rather than relying on the dependency's own timeout. The two distinct
    /// honouring mechanisms are exercised: StackExchange.Redis has no token parameter, so the cache reads honour
    /// it via <c>Task.WaitAsync</c>; EF Core / Npgsql honour the token natively on <c>SaveChangesAsync</c>.
    /// </summary>
    [Collection("Integration")]
    public class DataAccessCancellationTests : ApplicationIntegrationTestBase
    {
        public DataAccessCancellationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        private static CancellationToken AlreadyCancelled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            return cts.Token;
        }

        [Fact]
        public async Task GetPlayer_WhenTokenAlreadyCancelled_ThrowsOperationCanceled()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var playerRepo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();

            // The cache read (the first awaited I/O) is wrapped in WaitAsync, so a pre-cancelled token unwinds
            // it promptly instead of waiting out Redis's own command timeout.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => playerRepo.GetPlayer(playerEntity.Id, AlreadyCancelled()));
        }

        [Fact]
        public async Task GetCompletedChallengeIds_WhenTokenAlreadyCancelled_ThrowsOperationCanceled()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context);
            var playerEntity = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var progressRepo = scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>();

            // The zone-unlock gate (BattleService) reads progress through this cache-first path; the token now
            // reaches the cache read, so a cancelled budget unwinds it cooperatively.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => progressRepo.GetCompletedChallengeIds(playerEntity.Id, AlreadyCancelled()));
        }

        [Fact]
        public async Task CacheService_AsyncGet_WhenTokenAlreadyCancelled_ThrowsOperationCanceled()
        {
            using var scope = CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

            // The RedisService wraps each async operation in WaitAsync, so the token is honoured even though
            // StackExchange.Redis exposes no token parameter of its own.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => cache.Get("any-key", AlreadyCancelled()));
        }

        [Fact]
        public async Task CommitAsync_WithPendingChanges_WhenTokenAlreadyCancelled_ThrowsOperationCanceled()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Stage an unsaved insert so the commit has real work to do; EF Core / Npgsql honour the token
            // natively, so the cancelled commit throws rather than persisting the row.
            context.Users.Add(new User
            {
                Username = "cancel-commit",
                PassHash = "x",
                LastLogin = DateTime.UtcNow,
            });

            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => unitOfWork.CommitAsync(AlreadyCancelled()));
        }
    }
}
