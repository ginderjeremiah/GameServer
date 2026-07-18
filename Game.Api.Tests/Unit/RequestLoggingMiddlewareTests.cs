using Game.Abstractions.DataAccess;
using Game.Api.Middleware;
using Game.Api.Services;
using Game.Core.Players;
using Game.TestInfrastructure.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Covers the middleware's error-path observability guarantee: the "Request Ended" event (status +
    /// duration) is emitted even when the downstream pipeline throws, and the exception still propagates.
    /// </summary>
    public class RequestLoggingMiddlewareTests
    {
        [Fact]
        public async Task ThrowingPipeline_StillLogsRequestEnded_AndRethrows()
        {
            var capturingProvider = new CapturingLoggerProvider();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(capturingProvider));
            var logger = loggerFactory.CreateLogger<RequestLoggingMiddleware>();

            var thrown = new InvalidOperationException("downstream boom");
            RequestDelegate next = _ => throw thrown;
            var middleware = new RequestLoggingMiddleware(next, logger);

            var context = new DefaultHttpContext();
            context.Request.Method = "GET";
            context.Request.Path = "/api/Auth/Status";
            context.Response.StatusCode = 500;
            var sessionService = new SessionService(new NoOpSessionStore());

            var caught = await Assert.ThrowsAsync<InvalidOperationException>(
                () => middleware.InvokeAsync(context, sessionService));
            Assert.Same(thrown, caught);

            var category = typeof(RequestLoggingMiddleware).FullName;
            Assert.NotNull(category);
            var entries = capturingProvider.Entries.Where(e => e.Category == category).ToList();

            Assert.Single(entries, e => e.Message.StartsWith("Request Start"));

            var endedEntry = Assert.Single(entries, e => e.Message.StartsWith("Request Ended"));
            Assert.Equal(LogLevel.Information, endedEntry.Level);
            var statusCode = (int)Assert.IsType<int>(endedEntry.Properties.Single(p => p.Key == "StatusCode").Value);
            Assert.Equal(500, statusCode);
            var elapsedMs = (long)Assert.IsType<long>(endedEntry.Properties.Single(p => p.Key == "ElapsedMs").Value);
            Assert.True(elapsedMs >= 0);
        }

        private sealed class NoOpSessionStore : ISessionStore
        {
            public Task<PlayerState?> GetSession(int userId, CancellationToken cancellationToken = default) => Task.FromResult<PlayerState?>(null);
            public void Update(PlayerState sessionData, int userId) { }
            public Task UpdateAsync(PlayerState sessionData, int userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void Clear(int userId) { }
        }
    }
}
