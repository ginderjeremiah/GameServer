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
    /// Covers the per-client-IP auth rate limiter (#950): the anonymous auth endpoints are throttled with
    /// the project's standard error envelope once the configured limit is exceeded, the limit is a shared
    /// budget across those endpoints, and a request under the limit is unaffected. The factory pins the
    /// limit to a small value so the throttle can be exercised deterministically (the in-memory TestServer
    /// has no socket peer, so every request shares the "unknown" partition).
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
                var allowed = await Client.PostAsJsonAsync("/api/Login", creds, CancellationToken);
                Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
            }

            // The next attempt is throttled before the endpoint runs.
            var throttled = await Client.PostAsJsonAsync("/api/Login", creds, CancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
            Assert.True(throttled.Headers.Contains("Retry-After"));

            var body = await throttled.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(body);
            Assert.False(string.IsNullOrEmpty(body.ErrorMessage));
        }

        [Fact]
        public async Task AuthEndpoints_ShareOnePerIpBudget()
        {
            var creds = new { Username = "nobody", Password = "wrong" };

            // Exhaust the budget via Login.
            for (var i = 0; i < PermitLimit; i++)
            {
                await Client.PostAsJsonAsync("/api/Login", creds, CancellationToken);
            }

            // A sibling auth endpoint draws from the same per-IP partition, so it is already throttled.
            var createAccount = await Client.PostAsJsonAsync("/api/Login/CreateAccount", creds, CancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, createAccount.StatusCode);
        }

        [Fact]
        public async Task SingleRequest_UnderLimit_IsNotThrottled()
        {
            var response = await Client.PostAsJsonAsync(
                "/api/Login", new { Username = "nobody", Password = "wrong" }, CancellationToken);

            Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
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
