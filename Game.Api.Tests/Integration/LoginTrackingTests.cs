using Game.Api.Http;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class LoginTrackingTests : ApiIntegrationTestBase
    {
        private const string UserAgent = "TestAgent/1.0 (LoginTrackingTests)";
        // Well-formed (64 lowercase hex chars) — ClientHints.DeviceFingerprint now rejects anything else (#2064).
        private const string Fingerprint = "e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5e5";

        public LoginTrackingTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task AuthenticatedRequest_RecordsLoginDeviceAndBrowserInfo()
        {
            // Arrange — a real, logged-in user (the login itself is anonymous, so tracking fires on the
            // first authenticated request).
            var userId = await SeedUserAsync("trackuser", "trackpass");
            using var authClient = await LoginWithDeviceAsync("trackuser", "trackpass");

            // Act
            var response = await authClient.GetAsync("/api/Auth/Status", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert — a UserLogin, its Device, and the device's BrowserInfo were recorded for this user.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var login = await context.UserLogins.SingleAsync(l => l.UserId == userId, CancellationToken);
            Assert.False(string.IsNullOrEmpty(login.IpAddress));
            Assert.True(login.LastConnection > DateTime.UtcNow.AddMinutes(-1));

            var device = await context.Devices.SingleAsync(d => d.Id == login.DeviceId, CancellationToken);
            Assert.Equal(Fingerprint, device.DeviceFingerprintHash);

            var browser = await context.BrowserInfos.SingleAsync(b => b.Id == device.BrowserInfoId, CancellationToken);
            Assert.Equal(UserAgent, browser.UserAgent);
        }

        [Fact]
        public async Task RequestWithoutFingerprint_RecordsNothing()
        {
            // Arrange — authenticated, but the client sends no device fingerprint header.
            var userId = await SeedUserAsync("nofpuser", "nofppass");
            var (authClient, _) = await LoginAndBuildClientAsync("nofpuser", "nofppass");
            using var _client = authClient;

            // Act
            var response = await authClient.GetAsync("/api/Auth/Status", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert — without a fingerprint there is no device to key on, so nothing is recorded.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Empty(await context.UserLogins.Where(l => l.UserId == userId).ToListAsync(CancellationToken));
        }

        [Fact]
        public async Task RepeatedRequests_UpdateLastConnectionInPlace()
        {
            // Arrange
            var userId = await SeedUserAsync("repeatuser", "repeatpass");
            using var authClient = await LoginWithDeviceAsync("repeatuser", "repeatpass");

            // Act — two authenticated requests from the same user/IP/device.
            await authClient.GetAsync("/api/Auth/Status", CancellationToken);
            var firstConnection = await ReadLastConnectionAsync(userId);

            await authClient.GetAsync("/api/Auth/Status", CancellationToken);

            // Assert — still a single row (the combination is unique), with a non-decreasing timestamp.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var logins = await context.UserLogins.Where(l => l.UserId == userId).ToListAsync(CancellationToken);
            Assert.Single(logins);
            Assert.True(logins[0].LastConnection >= firstConnection);

            // And only one device + browser profile for the shared fingerprint/user-agent.
            Assert.Single(await context.Devices.Where(d => d.DeviceFingerprintHash == Fingerprint).ToListAsync(CancellationToken));
            Assert.Single(await context.BrowserInfos.Where(b => b.UserAgent == UserAgent).ToListAsync(CancellationToken));
        }

        [Fact]
        public async Task DeviceInfoEndpoint_EnrichesStoredDevice()
        {
            // Arrange
            await SeedUserAsync("enrichuser", "enrichpass");
            using var authClient = await LoginWithDeviceAsync("enrichuser", "enrichpass");

            var body = new { DeviceMemory = 16.0, HardwareConcurrency = 8 };

            // Act
            var response = await authClient.PostAsJsonAsync("/api/Auth/DeviceInfo", body, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert — the capabilities were applied to the Device for this request's fingerprint.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var device = await context.Devices.SingleAsync(d => d.DeviceFingerprintHash == Fingerprint, CancellationToken);
            Assert.Equal(16.0, device.DeviceMemory);
            Assert.Equal(8, device.HardwareConcurrency);
        }

        [Fact]
        public async Task DeviceInfoEndpoint_WithoutFingerprint_ReturnsError()
        {
            // Arrange — a logged-in client that omits the fingerprint header.
            await SeedUserAsync("nofpinfo", "nofpinfo");
            var (authClient, _) = await LoginAndBuildClientAsync("nofpinfo", "nofpinfo");
            using var _client = authClient;

            // Act
            var response = await authClient.PostAsJsonAsync("/api/Auth/DeviceInfo",
                new { DeviceMemory = 8.0, HardwareConcurrency = 4 }, CancellationToken);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UnauthenticatedRequest_RecordsNothing()
        {
            // Act — an anonymous request never reaches the authenticated tracking path.
            var response = await Client.GetAsync("/api/Auth/Status", CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            // Assert
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Empty(await context.UserLogins.ToListAsync(CancellationToken));
        }

        private async Task<int> SeedUserAsync(string username, string password)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, username, password);
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            // The caches no longer lazily refill, so reload them to resolve the player's linked skill on load.
            await ReloadReferenceCachesAsync();
            return user.Id;
        }

        private async Task<HttpClient> LoginWithDeviceAsync(string username, string password)
        {
            var (client, _) = await LoginAndBuildClientAsync(username, password);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
            client.DefaultRequestHeaders.TryAddWithoutValidation(ClientHints.DeviceFingerprintHeader, Fingerprint);
            return client;
        }

        private async Task<DateTime> ReadLastConnectionAsync(int userId)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var login = await context.UserLogins.SingleAsync(l => l.UserId == userId, CancellationToken);
            return login.LastConnection;
        }
    }
}
