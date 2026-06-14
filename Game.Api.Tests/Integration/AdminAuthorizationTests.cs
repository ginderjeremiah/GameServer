using Game.Abstractions.DataAccess;
using Game.Core;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Verifies that admin authorization (<c>AdminRoleAuthorizationFilter</c>) derives solely from the
    /// cryptographically validated JWT, not from the presence of the player's <c>Session_{userId}</c> cache
    /// key. A valid admin token must authorize even when no game session exists (e.g. the key was evicted,
    /// aged out under a sliding TTL — #537 — or the admin never established a game session), and a non-admin
    /// token must still be forbidden (#649).
    /// </summary>
    [Collection("Integration")]
    public class AdminAuthorizationTests : ApiIntegrationTestBase
    {
        public AdminAuthorizationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task AdminToken_WithNoSessionCacheKey_Authorizes()
        {
            const int userId = 4242;
            await AssertNoSessionKeyAsync(userId);

            // Token-only client: no CreateSession call, so no Session_{userId} key is written.
            var client = Factory.CreateClient();
            TestAuthHelper.AddAuthHeader(client, userId, nameof(ERole.Admin));

            var response = await client.GetAsync("/api/AdminTools/GetUsers", CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await AssertNoSessionKeyAsync(userId);
            client.Dispose();
        }

        [Fact]
        public async Task NonAdminToken_WithNoSessionCacheKey_IsForbidden()
        {
            const int userId = 4343;
            await AssertNoSessionKeyAsync(userId);

            var client = Factory.CreateClient();
            TestAuthHelper.AddAuthHeader(client, userId);

            var response = await client.GetAsync("/api/AdminTools/GetUsers", CancellationToken);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            client.Dispose();
        }

        /// <summary>
        /// Confirms the game-session cache key for the user is absent, reading through the same store the
        /// session loader uses rather than reconstructing the internal key format.
        /// </summary>
        private async Task AssertNoSessionKeyAsync(int userId)
        {
            using var scope = CreateScope();
            var sessionStore = scope.ServiceProvider.GetRequiredService<ISessionStore>();
            Assert.Null(await sessionStore.GetSession(userId));
        }
    }
}
