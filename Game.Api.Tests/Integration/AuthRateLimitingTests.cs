using Game.Api.Models.Common;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Covers the per-client-IP auth rate limiter (#950): every endpoint consuming credentials or a refresh
    /// token is throttled with the project's standard error envelope once the configured limit is exceeded,
    /// the limit is one shared budget across those endpoints, and a request under the limit is unaffected.
    /// The factory pins the limit to a small value so the throttle can be exercised deterministically (the
    /// in-memory TestServer has no socket peer, so every request shares the "unknown" partition).
    /// </summary>
    [Collection("Integration")]
    public class AuthRateLimitingTests : ApiIntegrationTestBase
    {
        private const int PermitLimit = 3;

        public AuthRateLimitingTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        protected override GameServerFactory CreateFactory(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
        {
            return new RateLimitedFactory(containers, testOutputHelper);
        }

        [Fact]
        public async Task Login_BeyondPermitLimit_IsThrottledWithEnvelope()
        {
            var creds = new { Username = "nobody", Password = "wrong" };

            // The first PermitLimit attempts run the endpoint (rejected as bad credentials, not throttled).
            for (var i = 0; i < PermitLimit; i++)
            {
                var allowed = await Client.PostAsJsonAsync("/api/Auth", creds, CancellationToken);
                Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
            }

            // The next attempt is throttled before the endpoint runs.
            var throttled = await Client.PostAsJsonAsync("/api/Auth", creds, CancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
            Assert.True(throttled.Headers.Contains("Retry-After"));

            var body = await throttled.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(body);
            Assert.False(string.IsNullOrEmpty(body.ErrorMessage));
        }

        [Fact]
        public async Task AuthEndpoints_ShareOnePerIpBudget()
        {
            await ExhaustBudgetViaLogin();

            // A sibling auth endpoint draws from the same per-IP partition, so it is already throttled.
            var createAccount = await Client.PostAsJsonAsync(
                "/api/Auth/CreateAccount", new { Username = "nobody", Password = "wrong" }, CancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, createAccount.StatusCode);
        }

        [Fact]
        public async Task Logout_DrawsFromThePerIpBudget()
        {
            await ExhaustBudgetViaLogin();

            // Logout is anonymous like its siblings, so it must also be throttled once the shared budget is
            // spent — closing the gap where it could be spammed unthrottled as a token-revocation surface.
            var loggedOut = await Client.PostAsJsonAsync(
                "/api/Auth/Logout", new { RefreshToken = "any-token" }, CancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, loggedOut.StatusCode);
        }

        // SelectPlayer/SwitchPlayer consume a raw refresh token just like Refresh/Logout do, so they must
        // draw from the same budget rather than offering an unthrottled probe of one (#2417). The limiter
        // runs ahead of authentication, so an over-budget call is rejected as 429 before the 401 it would
        // otherwise get — which is exactly what pins the endpoint into the policy.
        [Theory]
        [InlineData("/api/Players/SelectPlayer")]
        [InlineData("/api/Players/SwitchPlayer")]
        public async Task RefreshTokenConsumingPlayerEndpoints_DrawFromThePerIpBudget(string path)
        {
            await ExhaustBudgetViaLogin();

            var response = await Client.PostAsJsonAsync(
                path, new { PlayerId = 1, RefreshToken = "any-token" }, CancellationToken);

            Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        [Fact]
        public async Task PlayerEndpointsConsumingNoToken_StayOutsideThePerIpBudget()
        {
            await ExhaustBudgetViaLogin();

            // The policy is scoped to credential/token-consuming endpoints, so the rest of the pre-game
            // surface must not be collaterally throttled — it is rejected as unauthenticated instead.
            var response = await Client.PostAsJsonAsync(
                "/api/Players/CreatePlayer", new { Name = "Nobody", ClassId = 0 }, CancellationToken);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task SingleRequest_UnderLimit_IsNotThrottled()
        {
            var response = await Client.PostAsJsonAsync(
                "/api/Auth", new { Username = "nobody", Password = "wrong" }, CancellationToken);

            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
        }

        // Spends the whole per-IP window on Login, leaving the shared budget exhausted for whichever
        // sibling endpoint the caller is pinning.
        private async Task ExhaustBudgetViaLogin()
        {
            for (var i = 0; i < PermitLimit; i++)
            {
                await Client.PostAsJsonAsync(
                    "/api/Auth", new { Username = "nobody", Password = "wrong" }, CancellationToken);
            }
        }

        private sealed class RateLimitedFactory(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : GameServerFactory(containers, testOutputHelper)
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);

                // Registered after the base config, so this small limit overrides the suite-wide high one.
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["RateLimiting:Auth:PermitLimit"] = PermitLimit.ToString(),
                        ["RateLimiting:Auth:WindowSeconds"] = "60",
                    });
                });
            }
        }
    }
}
