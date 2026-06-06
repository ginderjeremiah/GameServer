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

        public LoginTrackingTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        [Fact]
        public async Task AuthenticatedRequest_RecordsLoginAndBrowserInfo()
        {
            // Arrange — a real, logged-in user (the login itself is anonymous, so tracking fires on the
            // first authenticated request).
            var userId = await SeedUserAsync("trackuser", "trackpass");
            using var authClient = await LoginWithUserAgentAsync("trackuser", "trackpass");

            // Act
            var response = await authClient.GetAsync("/api/Login/Status", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert — a UserLogin and its BrowserInfo were recorded for this user.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var login = await context.UserLogins.SingleAsync(l => l.UserId == userId, CancellationToken);
            Assert.False(string.IsNullOrEmpty(login.IpAddress));
            Assert.True(login.LastConnection > DateTime.UtcNow.AddMinutes(-1));

            var browser = await context.BrowserInfos.SingleAsync(b => b.Id == login.BrowserInfoId, CancellationToken);
            Assert.Equal(UserAgent, browser.UserAgent);
        }

        [Fact]
        public async Task RepeatedRequests_UpdateLastConnectionInPlace()
        {
            // Arrange
            var userId = await SeedUserAsync("repeatuser", "repeatpass");
            using var authClient = await LoginWithUserAgentAsync("repeatuser", "repeatpass");

            // Act — two authenticated requests from the same user/IP/browser.
            await authClient.GetAsync("/api/Login/Status", CancellationToken);
            var firstConnection = await ReadLastConnectionAsync(userId);

            await authClient.GetAsync("/api/Login/Status", CancellationToken);

            // Assert — still a single row (the combination is unique), with a non-decreasing timestamp.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var logins = await context.UserLogins.Where(l => l.UserId == userId).ToListAsync(CancellationToken);
            Assert.Single(logins);
            Assert.True(logins[0].LastConnection >= firstConnection);

            // And only one browser profile for the shared user-agent.
            var browsers = await context.BrowserInfos.Where(b => b.UserAgent == UserAgent).ToListAsync(CancellationToken);
            Assert.Single(browsers);
        }

        [Fact]
        public async Task BrowserInfoEndpoint_EnrichesStoredProfile()
        {
            // Arrange
            await SeedUserAsync("enrichuser", "enrichpass");
            using var authClient = await LoginWithUserAgentAsync("enrichuser", "enrichpass");

            var body = new { DeviceFingerprintHash = "abc123", DeviceMemory = 16.0, HardwareConcurrency = 8 };

            // Act
            var response = await authClient.PostAsJsonAsync("/api/Login/BrowserInfo", body, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert — the device signals were applied to the BrowserInfo for this request's user-agent.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var browser = await context.BrowserInfos.SingleAsync(b => b.UserAgent == UserAgent, CancellationToken);
            Assert.Equal("abc123", browser.DeviceFingerprintHash);
            Assert.Equal(16.0, browser.DeviceMemory);
            Assert.Equal(8, browser.HardwareConcurrency);
        }

        [Fact]
        public async Task UnauthenticatedRequest_RecordsNothing()
        {
            // Act — an anonymous request never reaches the authenticated tracking path.
            var response = await Client.GetAsync("/api/Login/Status", CancellationToken);
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
            return user.Id;
        }

        private async Task<HttpClient> LoginWithUserAgentAsync(string username, string password)
        {
            var (client, _) = await LoginAndBuildClientAsync(username, password);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
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
