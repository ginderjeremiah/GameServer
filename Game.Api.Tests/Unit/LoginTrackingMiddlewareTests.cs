using Game.Abstractions.DataAccess;
using Game.Api.Http;
using Game.Api.Middleware;
using Game.Api.Services;
using Game.Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Covers <see cref="LoginTrackingMiddleware"/>'s request-skip conditions and its per-(user, IP, device)
    /// dedupe memo (#1727): a genuinely new combination is always recorded, but repeat requests from the same
    /// combination within the dedupe window skip the multi-query DB upsert entirely, and a failed attempt is
    /// never memoized as done so it is retried on the next request.
    /// </summary>
    public class LoginTrackingMiddlewareTests
    {
        private const string Fingerprint = "fp-abc";
        private const string UserAgent = "TestAgent/1.0";
        private const string Ip = "203.0.113.7";

        [Fact]
        public async Task Unauthenticated_SkipsTrackingAndCallsNext()
        {
            var (middleware, userLogins, nextCalled) = CreateMiddleware();
            var session = new SessionService(new NoOpSessionStore());
            var context = CreateContext(Ip, Fingerprint, UserAgent);

            await middleware.InvokeAsync(context, session, ScopeFactory(userLogins), new MemoryCache(new MemoryCacheOptions()));

            Assert.Equal(0, userLogins.RecordConnectionCallCount);
            Assert.True(nextCalled());
        }

        [Fact]
        public async Task Authenticated_NoFingerprintHeader_SkipsTrackingAndCallsNext()
        {
            var (middleware, userLogins, nextCalled) = CreateMiddleware();
            var session = AuthenticatedSession(userId: 5);
            var context = CreateContext(Ip, fingerprint: null, UserAgent);

            await middleware.InvokeAsync(context, session, ScopeFactory(userLogins), new MemoryCache(new MemoryCacheOptions()));

            Assert.Equal(0, userLogins.RecordConnectionCallCount);
            Assert.True(nextCalled());
        }

        [Fact]
        public async Task Authenticated_NewDeviceIpUser_RecordsConnectionAndCallsNext()
        {
            var (middleware, userLogins, nextCalled) = CreateMiddleware();
            var session = AuthenticatedSession(userId: 5);
            var context = CreateContext(Ip, Fingerprint, UserAgent);

            await middleware.InvokeAsync(context, session, ScopeFactory(userLogins), new MemoryCache(new MemoryCacheOptions()));

            Assert.Equal(1, userLogins.RecordConnectionCallCount);
            Assert.True(nextCalled());
        }

        [Fact]
        public async Task Authenticated_RepeatRequestSameKey_SkipsTheSecondDbRoundTrip()
        {
            var (middleware, userLogins, _) = CreateMiddleware();
            var session = AuthenticatedSession(userId: 5);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var scopeFactory = ScopeFactory(userLogins);

            await middleware.InvokeAsync(CreateContext(Ip, Fingerprint, UserAgent), session, scopeFactory, cache);
            await middleware.InvokeAsync(CreateContext(Ip, Fingerprint, UserAgent), session, scopeFactory, cache);

            // Same (user, IP, device) within the dedupe window: only the first request hits the DB.
            Assert.Equal(1, userLogins.RecordConnectionCallCount);
        }

        [Theory]
        [InlineData("203.0.113.8", Fingerprint)]
        [InlineData(Ip, "fp-different")]
        public async Task Authenticated_DifferentIpOrDevice_RecordsSeparately(string ip, string fingerprint)
        {
            var (middleware, userLogins, _) = CreateMiddleware();
            var session = AuthenticatedSession(userId: 5);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var scopeFactory = ScopeFactory(userLogins);

            await middleware.InvokeAsync(CreateContext(Ip, Fingerprint, UserAgent), session, scopeFactory, cache);
            await middleware.InvokeAsync(CreateContext(ip, fingerprint, UserAgent), session, scopeFactory, cache);

            Assert.Equal(2, userLogins.RecordConnectionCallCount);
        }

        [Fact]
        public async Task Authenticated_DifferentUser_RecordsSeparately()
        {
            var (middleware, userLogins, _) = CreateMiddleware();
            var cache = new MemoryCache(new MemoryCacheOptions());
            var scopeFactory = ScopeFactory(userLogins);

            await middleware.InvokeAsync(CreateContext(Ip, Fingerprint, UserAgent), AuthenticatedSession(userId: 5), scopeFactory, cache);
            await middleware.InvokeAsync(CreateContext(Ip, Fingerprint, UserAgent), AuthenticatedSession(userId: 6), scopeFactory, cache);

            Assert.Equal(2, userLogins.RecordConnectionCallCount);
        }

        [Fact]
        public async Task RecordConnectionThrows_IsSwallowedAndNotMemoized_SoTheNextRequestRetries()
        {
            var (middleware, userLogins, nextCalled) = CreateMiddleware();
            userLogins.ThrowOnRecordConnection = true;
            var session = AuthenticatedSession(userId: 5);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var scopeFactory = ScopeFactory(userLogins);

            await middleware.InvokeAsync(CreateContext(Ip, Fingerprint, UserAgent), session, scopeFactory, cache);
            await middleware.InvokeAsync(CreateContext(Ip, Fingerprint, UserAgent), session, scopeFactory, cache);

            // A failed save is never memoized as done, so both requests attempted the DB call, and neither
            // failure broke the request itself.
            Assert.Equal(2, userLogins.RecordConnectionCallCount);
            Assert.True(nextCalled());
        }

        private static (LoginTrackingMiddleware Middleware, FakeUserLogins UserLogins, Func<bool> NextCalled) CreateMiddleware()
        {
            var nextCalled = false;
            RequestDelegate next = _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new LoginTrackingMiddleware(next, NullLogger<LoginTrackingMiddleware>.Instance);
            return (middleware, new FakeUserLogins(), () => nextCalled);
        }

        private static SessionService AuthenticatedSession(int userId)
        {
            var session = new SessionService(new NoOpSessionStore());
            session.SetAuthenticatedUser(userId);
            return session;
        }

        private static IServiceScopeFactory ScopeFactory(FakeUserLogins userLogins)
        {
            var provider = new ServiceCollection()
                .AddScoped<IUserLogins>(_ => userLogins)
                .AddScoped<LoginTrackingService>()
                .BuildServiceProvider();
            return provider.GetRequiredService<IServiceScopeFactory>();
        }

        private static DefaultHttpContext CreateContext(string ip, string? fingerprint, string userAgent)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            context.Request.Headers.UserAgent = userAgent;
            if (fingerprint is not null)
            {
                context.Request.Headers[ClientHints.DeviceFingerprintHeader] = fingerprint;
            }

            return context;
        }

        // Records how many times RecordConnection was actually invoked, so tests can tell whether the
        // dedupe memo skipped the DB round-trip. ThrowOnRecordConnection lets a test pin the failure path.
        private sealed class FakeUserLogins : IUserLogins
        {
            public int RecordConnectionCallCount { get; private set; }
            public bool ThrowOnRecordConnection { get; set; }

            public Task RecordConnection(
                int userId,
                string ipAddress,
                string deviceFingerprintHash,
                string userAgent,
                string? secChUa,
                string? secChUaMobile,
                string? secChUaPlatform,
                CancellationToken cancellationToken = default)
            {
                RecordConnectionCallCount++;
                if (ThrowOnRecordConnection)
                {
                    throw new InvalidOperationException("simulated tracking failure");
                }

                return Task.CompletedTask;
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

        private sealed class NoOpSessionStore : ISessionStore
        {
            public Task<Game.Core.Players.PlayerState?> GetSession(int userId, CancellationToken cancellationToken = default) =>
                Task.FromResult<Game.Core.Players.PlayerState?>(null);

            public void Update(Game.Core.Players.PlayerState sessionData, int userId) { }

            public Task UpdateAsync(Game.Core.Players.PlayerState sessionData, int userId, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public void Clear(int userId) { }
        }
    }
}
