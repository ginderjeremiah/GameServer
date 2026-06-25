using Game.Api.Http;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Shared setup for the trusted-proxy gate on the login-tracking IP (#910). The in-memory TestServer
    /// has no socket peer, so a fixed one is injected; subclasses decide whether that peer is a trusted
    /// forwarded-headers proxy. Asserts which IP is ultimately recorded for a login.
    /// </summary>
    public abstract class ForwardedHeadersTrustTestsBase : ApiIntegrationTestBase
    {
        protected const string Fingerprint = "fp-fwd-headers";
        protected const string SimulatedPeerIp = "203.0.113.9";
        protected const string SpoofedClientIp = "9.9.9.9";
        // A second proxy hop (e.g. a CDN sitting in front of the ingress) for the multi-hop ForwardLimit cases.
        protected const string IntermediateProxyIp = "203.0.113.50";

        protected ForwardedHeadersTrustTestsBase(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        /// <summary>The proxy IPs trusted by this scenario (empty trusts nothing).</summary>
        protected abstract IReadOnlyList<string> KnownProxies { get; }

        /// <summary>The trust-chain depth to configure; null leaves the deployment default (1) in place.</summary>
        protected virtual int? ForwardLimit => null;

        /// <summary>The X-Forwarded-For value the client sends; a single spoofed entry by default.</summary>
        protected virtual string ForwardedForHeader => SpoofedClientIp;

        protected override GameServerFactory CreateFactory(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
        {
            return new ForwardedHeadersFactory(containers, testOutputHelper, SimulatedPeerIp, KnownProxies, ForwardLimit);
        }

        protected async Task<string> ReadRecordedIpAsync(int userId)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var login = await context.UserLogins.SingleAsync(l => l.UserId == userId, CancellationToken);
            return login.IpAddress;
        }

        protected async Task<int> SeedUserAsync(string username, string password)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await TestDataSeeder.CreateUserAsync(context, username, password);
            var skill = await TestDataSeeder.CreateSkillAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            await TestDataSeeder.LinkSkillToPlayerAsync(context, player.Id, skill.Id);
            await ReloadReferenceCachesAsync();
            return user.Id;
        }

        protected async Task<HttpClient> LoginWithDeviceAsync(string username, string password)
        {
            var (client, _) = await LoginAndBuildClientAsync(username, password);
            client.DefaultRequestHeaders.TryAddWithoutValidation(ClientHints.DeviceFingerprintHeader, Fingerprint);
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", ForwardedForHeader);
            return client;
        }

        /// <summary>
        /// A factory that simulates a fixed socket peer address (the in-memory TestServer has none) and
        /// optionally configures it as a trusted forwarded-headers proxy.
        /// </summary>
        private sealed class ForwardedHeadersFactory(
            IntegrationTestContainers containers,
            ITestOutputHelper testOutputHelper,
            string simulatedPeerIp,
            IReadOnlyList<string> knownProxies,
            int? forwardLimit) : GameServerFactory(containers, testOutputHelper)
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);

                if (knownProxies.Count > 0 || forwardLimit is not null)
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        var settings = new Dictionary<string, string?>();
                        for (var i = 0; i < knownProxies.Count; i++)
                        {
                            settings[$"ForwardedHeaders:KnownProxies:{i}"] = knownProxies[i];
                        }
                        if (forwardLimit is not null)
                        {
                            settings["ForwardedHeaders:ForwardLimit"] = forwardLimit.Value.ToString();
                        }
                        config.AddInMemoryCollection(settings);
                    });
                }

                // Set a socket peer before the forwarded-headers middleware runs: the in-memory TestServer
                // leaves RemoteIpAddress null, and the forwarded-headers gate needs a peer to match against
                // the trusted-proxy allowlist.
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter(IPAddress.Parse(simulatedPeerIp)));
                });
            }
        }

        private sealed class RemoteIpStartupFilter(IPAddress peer) : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return app =>
                {
                    app.Use(async (context, nextMiddleware) =>
                    {
                        context.Connection.RemoteIpAddress = peer;
                        await nextMiddleware();
                    });
                    next(app);
                };
            }
        }
    }

    [Collection("Integration")]
    public class ForwardedHeadersUntrustedTests : ForwardedHeadersTrustTestsBase
    {
        public ForwardedHeadersUntrustedTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        // No proxy is trusted, so a spoofed X-Forwarded-For must be ignored.
        protected override IReadOnlyList<string> KnownProxies => [];

        [Fact]
        public async Task SpoofedForwardedFor_FromUntrustedPeer_IsIgnored()
        {
            var userId = await SeedUserAsync("fwduntrusted", "fwdpass");
            using var client = await LoginWithDeviceAsync("fwduntrusted", "fwdpass");

            var response = await client.GetAsync("/api/Login/Status", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var recordedIp = await ReadRecordedIpAsync(userId);
            Assert.Equal(SimulatedPeerIp, recordedIp);
            Assert.NotEqual(SpoofedClientIp, recordedIp);
        }
    }

    [Collection("Integration")]
    public class ForwardedHeadersTrustedTests : ForwardedHeadersTrustTestsBase
    {
        public ForwardedHeadersTrustedTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        // The simulated socket peer is configured as a known proxy, so its X-Forwarded-For is honoured.
        protected override IReadOnlyList<string> KnownProxies => [SimulatedPeerIp];

        [Fact]
        public async Task ForwardedFor_FromTrustedProxy_IsHonoured()
        {
            var userId = await SeedUserAsync("fwdtrusted", "fwdpass");
            using var client = await LoginWithDeviceAsync("fwdtrusted", "fwdpass");

            var response = await client.GetAsync("/api/Login/Status", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var recordedIp = await ReadRecordedIpAsync(userId);
            Assert.Equal(SpoofedClientIp, recordedIp);
        }
    }

    [Collection("Integration")]
    public class ForwardedHeadersMultiHopTests : ForwardedHeadersTrustTestsBase
    {
        public ForwardedHeadersMultiHopTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        // Both hops of a CDN → ingress chain are trusted and ForwardLimit matches the chain depth (2),
        // so the walk reaches the original client entry rather than stopping at the intermediate proxy.
        protected override IReadOnlyList<string> KnownProxies => [SimulatedPeerIp, IntermediateProxyIp];
        protected override int? ForwardLimit => 2;
        protected override string ForwardedForHeader => $"{SpoofedClientIp}, {IntermediateProxyIp}";

        [Fact]
        public async Task ForwardedFor_AcrossTrustedChain_ResolvesTheRealClient()
        {
            var userId = await SeedUserAsync("fwdmultihop", "fwdpass");
            using var client = await LoginWithDeviceAsync("fwdmultihop", "fwdpass");

            var response = await client.GetAsync("/api/Login/Status", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var recordedIp = await ReadRecordedIpAsync(userId);
            Assert.Equal(SpoofedClientIp, recordedIp);
        }
    }

    [Collection("Integration")]
    public class ForwardedHeadersDefaultLimitMultiHopTests : ForwardedHeadersTrustTestsBase
    {
        public ForwardedHeadersDefaultLimitMultiHopTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        // Both hops are trusted but the default ForwardLimit (1) is left in place, so the walk stops at the
        // intermediate proxy — the real client is never reached. This pins the exact behaviour #1236 fixes:
        // a multi-hop deployment must raise ForwardLimit to reach the true client IP.
        protected override IReadOnlyList<string> KnownProxies => [SimulatedPeerIp, IntermediateProxyIp];
        protected override string ForwardedForHeader => $"{SpoofedClientIp}, {IntermediateProxyIp}";

        [Fact]
        public async Task ForwardedFor_WithDefaultLimit_StopsAtTheIntermediateProxy()
        {
            var userId = await SeedUserAsync("fwddefaultlimit", "fwdpass");
            using var client = await LoginWithDeviceAsync("fwddefaultlimit", "fwdpass");

            var response = await client.GetAsync("/api/Login/Status", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var recordedIp = await ReadRecordedIpAsync(userId);
            Assert.Equal(IntermediateProxyIp, recordedIp);
            Assert.NotEqual(SpoofedClientIp, recordedIp);
        }
    }
}
