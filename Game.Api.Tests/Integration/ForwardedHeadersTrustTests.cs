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

        protected ForwardedHeadersTrustTestsBase(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        protected abstract string? KnownProxy { get; }

        protected override GameServerFactory CreateFactory(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
        {
            return new ForwardedHeadersFactory(containers, testOutputHelper, SimulatedPeerIp, KnownProxy);
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
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", SpoofedClientIp);
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
            string? knownProxy) : GameServerFactory(containers, testOutputHelper)
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);

                if (knownProxy is not null)
                {
                    builder.ConfigureAppConfiguration((_, config) =>
                    {
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ForwardedHeaders:KnownProxies:0"] = knownProxy,
                        });
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
        protected override string? KnownProxy => null;

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
        protected override string? KnownProxy => SimulatedPeerIp;

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
}
