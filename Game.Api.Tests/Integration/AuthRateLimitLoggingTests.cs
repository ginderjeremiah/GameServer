using Game.Api.RateLimiting;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Covers #2375: <c>UseRateLimiter</c> runs before <c>UseRequestLogging</c>, so a 429 short-circuits
    /// before the structured request log ever fires. The rejection handler emits its own log entry instead.
    /// </summary>
    [Collection("Integration")]
    public class AuthRateLimitLoggingTests : ApiIntegrationTestBase
    {
        private const int PermitLimit = 3;

        // Field initializer runs before the base constructor so _capturingProvider is ready
        // when CreateFactory is called from the base constructor.
        private readonly CapturingLoggerProvider _capturingProvider = new();

        private static readonly string LoggerCategory = typeof(RateLimiterServiceCollectionExtensions).FullName!;

        public AuthRateLimitLoggingTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        protected override GameServerFactory CreateFactory(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
        {
            return new RateLimitedFactory(containers, testOutputHelper, _capturingProvider);
        }

        [Fact]
        public async Task Rejection_LogsWarningWithPathAndIpPartitionKey()
        {
            var creds = new { Username = "nobody", Password = "wrong" };

            for (var i = 0; i < PermitLimit; i++)
            {
                await Client.PostAsJsonAsync("/api/Auth", creds, CancellationToken);
            }

            var startIndex = _capturingProvider.Entries.Count;
            var throttled = await Client.PostAsJsonAsync("/api/Auth", creds, CancellationToken);

            var entry = Assert.Single(
                _capturingProvider.Entries.Skip(startIndex), e => e.Category == LoggerCategory);

            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains("Rate limit rejected", entry.Message);
            Assert.Contains("/api/Auth", entry.Message);
            Assert.Equal("IP", entry.Properties.Single(p => p.Key == "PartitionKeyType").Value);
            // The in-memory TestServer has no socket peer, so every request shares the "unknown" partition
            // key — this only pins that *some* IP-derived value (never the request body's credentials) is
            // logged, not a real address.
            Assert.NotNull(entry.Properties.Single(p => p.Key == "PartitionKey").Value);
        }

        private sealed class RateLimitedFactory(
            IntegrationTestContainers containers, ITestOutputHelper testOutputHelper, CapturingLoggerProvider capturingProvider)
            : GameServerFactory(containers, testOutputHelper, [capturingProvider])
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
