using Game.Abstractions.DataAccess;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Integration coverage for the connection-tracking get-or-create race (#907): concurrent connections
    /// from the same brand-new device/browser (multi-tab, rapid reconnect) must converge on a single
    /// deduplicated row set rather than throwing an unhandled unique violation or inserting duplicates. The
    /// unique indexes turn a lost read-then-insert into a conflict the repository catches and reloads.
    /// </summary>
    [Collection("Integration")]
    public class UserLoginsRaceIntegrationTests : ApplicationIntegrationTestBase
    {
        private const string Fingerprint = "fp-race";
        private const string UserAgent = "RaceAgent/1.0";
        private const string Ip = "203.0.113.7";
        private const int Concurrency = 12;

        public UserLoginsRaceIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task RecordConnection_ConcurrentFromSameNewDevice_ConvergesToSingleRowSet()
        {
            int userId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var user = await TestDataSeeder.CreateUserAsync(context);
                userId = user.Id;
            }

            // No call throws an unhandled unique violation — each conflict is caught and reloaded.
            await RunConcurrently(repo =>
                repo.RecordConnection(userId, Ip, Fingerprint, UserAgent, null, null, null, CancellationToken));

            using var scope = CreateScope();
            var assertContext = scope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Single(await assertContext.Devices.Where(d => d.DeviceFingerprintHash == Fingerprint).ToListAsync(CancellationToken));
            Assert.Single(await assertContext.BrowserInfos.Where(b => b.UserAgent == UserAgent).ToListAsync(CancellationToken));
            Assert.Single(await assertContext.UserLogins.Where(l => l.UserId == userId).ToListAsync(CancellationToken));
        }

        [Fact]
        public async Task SaveDeviceInfo_ConcurrentFromSameNewDevice_ConvergesToSingleDevice()
        {
            await RunConcurrently(repo =>
                repo.SaveDeviceInfo(Fingerprint, UserAgent, null, null, null, 8, 4, CancellationToken));

            using var scope = CreateScope();
            var assertContext = scope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Single(await assertContext.Devices.Where(d => d.DeviceFingerprintHash == Fingerprint).ToListAsync(CancellationToken));
            Assert.Single(await assertContext.BrowserInfos.Where(b => b.UserAgent == UserAgent).ToListAsync(CancellationToken));
        }

        /// <summary>
        /// Fires <see cref="Concurrency"/> copies of the operation, each on its own scope/DbContext, releasing
        /// them together so their reads race before any save commits. Faults surface through <c>Task.WhenAll</c>.
        /// </summary>
        private async Task RunConcurrently(Func<IUserLogins, Task> operation)
        {
            using var gate = new SemaphoreSlim(0, Concurrency);

            var tasks = Enumerable.Range(0, Concurrency).Select(async _ =>
            {
                using var scope = CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IUserLogins>();
                await gate.WaitAsync(CancellationToken);
                await operation(repo);
            }).ToList();

            gate.Release(Concurrency);
            await Task.WhenAll(tasks);
        }
    }
}
