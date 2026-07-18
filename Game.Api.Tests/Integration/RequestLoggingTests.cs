using Game.Api.Middleware;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using Xunit;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class RequestLoggingTests : ApiIntegrationTestBase
    {
        // Field initializer runs before the base constructor so _capturingProvider is ready
        // when CreateFactory is called from the base constructor.
        private readonly CapturingLoggerProvider _capturingProvider = new();

        private static readonly string LoggerCategory = typeof(RequestLoggingMiddleware).FullName!;

        public RequestLoggingTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        protected override GameServerFactory CreateFactory(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
        {
            return new GameServerFactory(containers, testOutputHelper, [_capturingProvider]);
        }

        [Fact]
        public async Task UnauthenticatedRequest_LogsRequestStartWithNullUserId()
        {
            var startIndex = _capturingProvider.Entries.Count;

            await Client.GetAsync("/api/Auth/Status", CancellationToken);

            var entries = GetMiddlewareEntries(startIndex);
            var startEntry = Assert.Single(entries, e => e.Message.StartsWith("Request Start"));
            var scope = Assert.IsType<Dictionary<string, object?>>(startEntry.ScopeStates.Single());
            Assert.Equal("GET", scope["Method"]);
            Assert.Equal("/api/Auth/Status", scope["Path"]);
            Assert.Null(scope["UserId"]);
            Assert.NotNull(scope["RequestId"]);
        }

        [Fact]
        public async Task UnauthenticatedRequest_LogsRequestEndedWithStatusCodeAndElapsedTime()
        {
            var startIndex = _capturingProvider.Entries.Count;

            var response = await Client.GetAsync("/api/Auth/Status", CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            var entries = GetMiddlewareEntries(startIndex);
            var endedEntry = Assert.Single(entries, e => e.Message.StartsWith("Request Ended"));
            Assert.Equal(LogLevel.Information, endedEntry.Level);

            var statusCode = (int)endedEntry.Properties.Single(p => p.Key == "StatusCode").Value!;
            Assert.Equal(401, statusCode);

            var elapsedMs = (long)endedEntry.Properties.Single(p => p.Key == "ElapsedMs").Value!;
            Assert.True(elapsedMs >= 0);
        }

        [Fact]
        public async Task AuthenticatedRequest_LogsUserIdInScope()
        {
            var userId = await SeedUserAsync("logtest_userid", "testpass");
            var login = await LoginAndBuildClientAsync("logtest_userid", "testpass");
            using var authClient = login.Client;

            var startIndex = _capturingProvider.Entries.Count;
            var response = await authClient.GetAsync("/api/Auth/Status", CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var entries = GetMiddlewareEntries(startIndex);
            var startEntry = Assert.Single(entries, e => e.Message.StartsWith("Request Start"));
            var scope = Assert.IsType<Dictionary<string, object?>>(startEntry.ScopeStates.Single());
            Assert.Equal(userId, scope["UserId"]);
        }

        [Fact]
        public async Task AuthenticatedRequest_LogsRequestEndedWithStatusCode200()
        {
            await SeedUserAsync("logtest_status200", "testpass");
            var login = await LoginAndBuildClientAsync("logtest_status200", "testpass");
            using var authClient = login.Client;

            var startIndex = _capturingProvider.Entries.Count;
            await authClient.GetAsync("/api/Auth/Status", CancellationToken);

            var entries = GetMiddlewareEntries(startIndex);
            var endedEntry = Assert.Single(entries, e => e.Message.StartsWith("Request Ended"));
            var statusCode = (int)endedEntry.Properties.Single(p => p.Key == "StatusCode").Value!;
            Assert.Equal(200, statusCode);
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

        private IReadOnlyList<CapturingEntry> GetMiddlewareEntries(int startIndex)
        {
            return _capturingProvider.Entries
                .Skip(startIndex)
                .Where(e => e.Category == LoggerCategory)
                .ToList();
        }
    }
}
