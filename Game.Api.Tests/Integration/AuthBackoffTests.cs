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
    /// Covers the per-account login backoff (#1010) at the HTTP edge: once an account has accrued too many
    /// consecutive failures it is rejected with a 429 carrying a Retry-After hint and a client-facing message
    /// distinct from the 400 a plain invalid-credentials failure returns. The factory pins a zero threshold so
    /// the very first failure arms the lock and the second attempt is backed off; the suite-wide IP rate
    /// limiter stays effectively disabled (a high permit limit) so this exercises the account backoff, not it.
    /// </summary>
    [Collection("Integration")]
    public class AuthBackoffTests : ApiIntegrationTestBase
    {
        public AuthBackoffTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        protected override GameServerFactory CreateFactory(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
        {
            return new BackoffFactory(containers, testOutputHelper);
        }

        [Fact]
        public async Task Login_AfterConsecutiveFailures_IsBackedOffWithDistinct429()
        {
            var creds = new { Username = "backofftarget", Password = "wrong" };

            // The first failure runs the credential check — a plain 400 invalid-credentials rejection.
            var firstAttempt = await Client.PostAsJsonAsync("/api/Auth", creds, CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, firstAttempt.StatusCode);
            var firstBody = await firstAttempt.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(firstBody);

            // The next attempt is within the backoff window: a 429 with a Retry-After hint and a message
            // distinct from the invalid-credentials one above.
            var backedOff = await Client.PostAsJsonAsync("/api/Auth", creds, CancellationToken);
            Assert.Equal(HttpStatusCode.TooManyRequests, backedOff.StatusCode);
            Assert.True(backedOff.Headers.Contains("Retry-After"));

            var backedOffBody = await backedOff.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.NotNull(backedOffBody);
            Assert.False(string.IsNullOrEmpty(backedOffBody.ErrorMessage));
            Assert.NotEqual(firstBody.ErrorMessage, backedOffBody.ErrorMessage);
        }

        private sealed class BackoffFactory(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : GameServerFactory(containers, testOutputHelper)
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);

                // Registered after the base config so these override the suite-wide defaults: a zero threshold
                // arms the lock on the first failure, while the IP rate limiter stays effectively off (base).
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["LoginBackoff:FailureThreshold"] = "0",
                        ["LoginBackoff:BaseDelaySeconds"] = "2",
                        ["LoginBackoff:MaxDelaySeconds"] = "4",
                        ["LoginBackoff:FailureWindowSeconds"] = "60",
                    });
                });
            }
        }
    }
}
