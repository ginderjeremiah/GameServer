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
    /// Integration coverage for the device/login-tracking hardening (#2064, #2240): a single account can't
    /// grow the Devices/UserLogins tables without bound by cycling fresh fingerprints or IPs, and
    /// <c>SaveDeviceInfo</c> can only enrich a device the caller actually has a tracked login for.
    /// </summary>
    [Collection("Integration")]
    public class UserLoginsCapAndOwnershipIntegrationTests : ApplicationIntegrationTestBase
    {
        private const int MaxTrackedDevicesPerUser = 20;
        private const int MaxTrackedIpsPerUserDevice = 20;
        private const string UserAgent = "TestAgent/1.0";
        private const string Ip = "203.0.113.10";

        public UserLoginsCapAndOwnershipIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task RecordConnection_PastDeviceCap_StopsTrackingNewDevicesButStillUpdatesKnownOnes()
        {
            int userId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var user = await TestDataSeeder.CreateUserAsync(context);
                userId = user.Id;
            }

            // Reach the cap with distinct devices.
            for (var i = 0; i < MaxTrackedDevicesPerUser; i++)
            {
                using var scope = CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IUserLogins>();
                await repo.RecordConnection(userId, Ip, Fingerprint(i), UserAgent, null, null, null, CancellationToken);
            }

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                Assert.Equal(MaxTrackedDevicesPerUser, await context.UserLogins.CountAsync(l => l.UserId == userId, CancellationToken));
            }

            // One more, brand-new device: silently not tracked rather than growing past the cap.
            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IUserLogins>();
                await repo.RecordConnection(userId, Ip, Fingerprint(MaxTrackedDevicesPerUser), UserAgent, null, null, null, CancellationToken);
            }

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                Assert.Equal(MaxTrackedDevicesPerUser, await context.UserLogins.CountAsync(l => l.UserId == userId, CancellationToken));
                Assert.Empty(await context.Devices.Where(d => d.DeviceFingerprintHash == Fingerprint(MaxTrackedDevicesPerUser)).ToListAsync(CancellationToken));
            }

            // A device the user already has (from a new IP) is exempt from the cap — it doesn't grow the count.
            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IUserLogins>();
                await repo.RecordConnection(userId, "203.0.113.11", Fingerprint(0), UserAgent, null, null, null, CancellationToken);
            }

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                Assert.Equal(MaxTrackedDevicesPerUser + 1, await context.UserLogins.CountAsync(l => l.UserId == userId, CancellationToken));
            }
        }

        [Fact]
        public async Task RecordConnection_PastIpCapForKnownDevice_StopsTrackingNewIpsButStillUpdatesKnownOnes()
        {
            int userId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var user = await TestDataSeeder.CreateUserAsync(context);
                userId = user.Id;
            }

            // Reach the cap with distinct IPs against the same, already-tracked device.
            for (var i = 0; i < MaxTrackedIpsPerUserDevice; i++)
            {
                using var scope = CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IUserLogins>();
                await repo.RecordConnection(userId, IpForIndex(i), Fingerprint(0), UserAgent, null, null, null, CancellationToken);
            }

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                Assert.Equal(MaxTrackedIpsPerUserDevice, await context.UserLogins.CountAsync(l => l.UserId == userId, CancellationToken));
            }

            // One more, brand-new IP for the same device: silently not tracked rather than growing past the cap.
            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IUserLogins>();
                await repo.RecordConnection(userId, IpForIndex(MaxTrackedIpsPerUserDevice), Fingerprint(0), UserAgent, null, null, null, CancellationToken);
            }

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                Assert.Equal(MaxTrackedIpsPerUserDevice, await context.UserLogins.CountAsync(l => l.UserId == userId, CancellationToken));
                Assert.Empty(await context.UserLogins
                    .Where(l => l.UserId == userId && l.IpAddress == IpForIndex(MaxTrackedIpsPerUserDevice))
                    .ToListAsync(CancellationToken));
            }

            // An already-tracked (user, IP, device) combination is the fast-path update, not blocked by a
            // cap that only governs *new* IPs — the row count stays put.
            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IUserLogins>();
                await repo.RecordConnection(userId, IpForIndex(0), Fingerprint(0), UserAgent, null, null, null, CancellationToken);
            }

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                Assert.Equal(MaxTrackedIpsPerUserDevice, await context.UserLogins.CountAsync(l => l.UserId == userId, CancellationToken));
            }
        }

        [Fact]
        public async Task SaveDeviceInfo_NoTrackedLoginForFingerprint_DoesNotCreateOrEnrichAnyDevice()
        {
            int userId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                var user = await TestDataSeeder.CreateUserAsync(context);
                userId = user.Id;
            }

            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IUserLogins>();
                await repo.SaveDeviceInfo(userId, Fingerprint(0), null, null, null, 16, 8, CancellationToken);
            }

            using var assertScope = CreateScope();
            var assertContext = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Empty(await assertContext.Devices.Where(d => d.DeviceFingerprintHash == Fingerprint(0)).ToListAsync(CancellationToken));
        }

        [Fact]
        public async Task SaveDeviceInfo_AnotherUsersDevice_DoesNotEnrichIt()
        {
            int ownerId;
            int otherId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                ownerId = (await TestDataSeeder.CreateUserAsync(context, "device-owner", "pass")).Id;
                otherId = (await TestDataSeeder.CreateUserAsync(context, "not-the-owner", "pass")).Id;
            }

            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IUserLogins>();
                await repo.RecordConnection(ownerId, Ip, Fingerprint(0), UserAgent, null, null, null, CancellationToken);
            }

            // A different account (which has never connected from this device) tries to enrich it.
            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IUserLogins>();
                await repo.SaveDeviceInfo(otherId, Fingerprint(0), null, null, null, 999, 999, CancellationToken);
            }

            using var assertScope = CreateScope();
            var assertContext = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var device = await assertContext.Devices.SingleAsync(d => d.DeviceFingerprintHash == Fingerprint(0), CancellationToken);
            Assert.Null(device.DeviceMemory);
            Assert.Null(device.HardwareConcurrency);
        }

        [Fact]
        public async Task SaveDeviceInfo_OwnDevice_EnrichesIt()
        {
            int userId;
            using (var seedScope = CreateScope())
            {
                var context = seedScope.ServiceProvider.GetRequiredService<GameContext>();
                userId = (await TestDataSeeder.CreateUserAsync(context)).Id;
            }

            using (var scope = CreateScope())
            {
                var repo = scope.ServiceProvider.GetRequiredService<IUserLogins>();
                await repo.RecordConnection(userId, Ip, Fingerprint(0), UserAgent, null, null, null, CancellationToken);
                await repo.SaveDeviceInfo(userId, Fingerprint(0), null, null, null, 16, 8, CancellationToken);
            }

            using var assertScope = CreateScope();
            var assertContext = assertScope.ServiceProvider.GetRequiredService<GameContext>();
            var device = await assertContext.Devices.SingleAsync(d => d.DeviceFingerprintHash == Fingerprint(0), CancellationToken);
            Assert.Equal(16, device.DeviceMemory);
            Assert.Equal(8, device.HardwareConcurrency);
        }

        // A distinct, deterministic fingerprint per index — real fingerprints are 64 lowercase hex chars, but
        // the repository itself doesn't enforce that shape (only the HTTP-layer ClientHints does), so a
        // simple distinguishable string is enough here.
        private static string Fingerprint(int index) => $"fp-cap-{index:D3}";

        // A distinct IP per index, drawn from the TEST-NET-2 documentation range (RFC 5737) so it can't
        // collide with the class-level Ip constant used elsewhere in this file.
        private static string IpForIndex(int index) => $"198.51.100.{index}";
    }
}
