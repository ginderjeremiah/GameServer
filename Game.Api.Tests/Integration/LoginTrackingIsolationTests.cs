using Game.Abstractions.DataAccess;
using Game.Api.Http;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using Xunit;

namespace Game.Api.Tests.Integration
{
    /// <summary>
    /// Pins the isolation guarantee of <c>LoginTrackingMiddleware</c> (#1230): because connection tracking
    /// self-commits and its failures are swallowed, it must run on its own <c>GameContext</c> — otherwise a
    /// non-unique save failure leaves queued inserts on the request's shared context that the per-action
    /// commit filter then re-flushes (a 500 on an unrelated request) or silently persists. The fault double
    /// reproduces that failure mode; the test asserts the unrelated request is unaffected and nothing leaks.
    /// </summary>
    [Collection("Integration")]
    public class LoginTrackingIsolationTests : ApiIntegrationTestBase
    {
        private const string UserAgent = "TestAgent/1.0 (LoginTrackingIsolationTests)";
        private const string Fingerprint = "fp-isolation";

        public LoginTrackingIsolationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        protected override GameServerFactory CreateFactory(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
        {
            return new FaultInjectingFactory(containers, testOutputHelper);
        }

        [Fact]
        public async Task TrackingSaveFailure_DoesNotBreakTheRequest_OrPersistAnything()
        {
            // Arrange — a real, logged-in user whose first authenticated request triggers tracking, which the
            // injected double makes fail with a non-unique (FK-violation) save error.
            var userId = await SeedUserAsync("isouser", "isopass");
            using var authClient = await LoginWithDeviceAsync("isouser", "isopass");

            // Act — an unrelated, read-only request. Its commit must not inherit the failed tracking inserts.
            var response = await authClient.GetAsync("/api/Auth/Status", CancellationToken);

            // Assert — the tracking failure was swallowed and isolated to its own scope, so the request still
            // succeeds rather than 500-ing on a re-flushed bad insert.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // And nothing the failed tracking queued rode along onto the request's unit of work.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            Assert.Empty(await context.UserLogins.Where(l => l.UserId == userId).ToListAsync(CancellationToken));
        }

        private async Task<int> SeedUserAsync(string username, string password)
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

        private async Task<HttpClient> LoginWithDeviceAsync(string username, string password)
        {
            var (client, _) = await LoginAndBuildClientAsync(username, password);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
            client.DefaultRequestHeaders.TryAddWithoutValidation(ClientHints.DeviceFingerprintHeader, Fingerprint);
            return client;
        }

        private sealed class FaultInjectingFactory(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : GameServerFactory(containers, testOutputHelper)
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);
                builder.ConfigureServices(services =>
                    services.Replace(ServiceDescriptor.Scoped<IUserLogins, FaultInjectingUserLogins>()));
            }
        }

        /// <summary>
        /// Reproduces the issue's failure mode: queue the tracking inserts on the context, then hit a
        /// non-unique save error — a <c>UserLogin</c> pointing at a nonexistent device, which violates the
        /// device foreign key. The real repo only recovers from unique violations, so this propagates.
        /// </summary>
        private sealed class FaultInjectingUserLogins(GameContext context) : IUserLogins
        {
            public async Task RecordConnection(
                int userId,
                string ipAddress,
                string deviceFingerprintHash,
                string userAgent,
                string? secChUa,
                string? secChUaMobile,
                string? secChUaPlatform,
                CancellationToken cancellationToken = default)
            {
                context.UserLogins.Add(new UserLogin
                {
                    UserId = userId,
                    IpAddress = ipAddress,
                    DeviceId = int.MaxValue,
                    LastConnection = DateTime.UtcNow,
                });
                await context.SaveChangesAsync(cancellationToken);
            }

            public Task SaveDeviceInfo(
                string deviceFingerprintHash,
                string userAgent,
                string? secChUa,
                string? secChUaMobile,
                string? secChUaPlatform,
                double? deviceMemory,
                int? hardwareConcurrency,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }
    }
}
