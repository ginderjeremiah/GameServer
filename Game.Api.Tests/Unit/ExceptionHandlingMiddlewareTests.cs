using Game.Api.Middleware;
using Game.Api.Models.Common;
using Game.TestInfrastructure.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Covers the centralized exception handler: an unhandled exception becomes a consistent 500
    /// <see cref="ApiResponse"/> envelope, the exception is logged exactly once, internal details only leak
    /// in Development, client aborts are not treated as errors, and an already-started response is left to
    /// unwind. The final case composes it with <see cref="RequestLoggingMiddleware"/> to confirm the logged
    /// end-event status reflects the handler's 500.
    /// </summary>
    public class ExceptionHandlingMiddlewareTests
    {
        private static readonly string LoggerCategory = typeof(ExceptionHandlingMiddleware).FullName!;

        [Fact]
        public async Task UnhandledException_Returns500_WithConsistentErrorEnvelope()
        {
            var (_, logger) = CreateLogger();
            RequestDelegate next = _ => throw new InvalidOperationException("boom");
            var middleware = new ExceptionHandlingMiddleware(next, logger, CreateEnvironment(Environments.Production));
            var context = CreateContextWithResponseBody();

            await middleware.InvokeAsync(context);

            Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
            Assert.NotNull(context.Response.ContentType);
            Assert.StartsWith("application/json", context.Response.ContentType);
            var body = await ReadErrorMessage(context);
            Assert.Equal("Internal Server Error", body);
        }

        [Fact]
        public async Task NonDevelopment_DoesNotLeakExceptionMessage()
        {
            var (_, logger) = CreateLogger();
            RequestDelegate next = _ => throw new InvalidOperationException("secret internal detail");
            var middleware = new ExceptionHandlingMiddleware(next, logger, CreateEnvironment(Environments.Production));
            var context = CreateContextWithResponseBody();

            await middleware.InvokeAsync(context);

            var body = await ReadErrorMessage(context);
            Assert.Equal("Internal Server Error", body);
            Assert.DoesNotContain("secret internal detail", body);
        }

        [Fact]
        public async Task Development_IncludesExceptionMessage()
        {
            var (_, logger) = CreateLogger();
            RequestDelegate next = _ => throw new InvalidOperationException("helpful dev detail");
            var middleware = new ExceptionHandlingMiddleware(next, logger, CreateEnvironment(Environments.Development));
            var context = CreateContextWithResponseBody();

            await middleware.InvokeAsync(context);

            Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
            var body = await ReadErrorMessage(context);
            Assert.Equal("helpful dev detail", body);
        }

        [Fact]
        public async Task UnhandledException_IsLoggedExactlyOnce_AtErrorLevel()
        {
            var (provider, logger) = CreateLogger();
            RequestDelegate next = _ => throw new InvalidOperationException("boom");
            var middleware = new ExceptionHandlingMiddleware(next, logger, CreateEnvironment(Environments.Production));
            var context = CreateContextWithResponseBody();

            await middleware.InvokeAsync(context);

            var errorEntry = Assert.Single(MiddlewareEntries(provider), e => e.Level == LogLevel.Error);
            Assert.Contains("Unhandled exception", errorEntry.Message);
        }

        [Fact]
        public async Task ClientAbort_IsSwallowed_WithoutErrorLogOrErrorResponse()
        {
            var (provider, logger) = CreateLogger();
            var middleware = new ExceptionHandlingMiddleware(
                _ => throw new OperationCanceledException(),
                logger,
                CreateEnvironment(Environments.Production));
            var context = CreateContextWithResponseBody();
            context.RequestAborted = new CancellationToken(canceled: true);

            // No exception escapes — the client disconnect is handled, not rethrown.
            await middleware.InvokeAsync(context);

            // The status was never overwritten to 500 and nothing is logged at Error level.
            Assert.NotEqual(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
            Assert.DoesNotContain(MiddlewareEntries(provider), e => e.Level == LogLevel.Error);
            Assert.Single(MiddlewareEntries(provider), e => e.Level == LogLevel.Debug);
        }

        [Fact]
        public async Task ResponseAlreadyStarted_Rethrows()
        {
            var (provider, logger) = CreateLogger();
            var thrown = new InvalidOperationException("after response started");
            RequestDelegate next = _ => throw thrown;
            var middleware = new ExceptionHandlingMiddleware(next, logger, CreateEnvironment(Environments.Production));

            // A response feature reporting HasStarted = true models bytes already on the wire (e.g. an
            // upgraded WebSocket), so the handler cannot replace the response and must let it unwind.
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature { Method = "GET", Path = "/socket" });
            features.Set<IHttpResponseFeature>(new StartedResponseFeature());
            features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(Stream.Null));
            var context = new DefaultHttpContext(features);

            var caught = await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
            Assert.Same(thrown, caught);
            // It is still logged once before being rethrown.
            Assert.Single(MiddlewareEntries(provider), e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task ComposedWithRequestLogging_LogsRequestEndedWith500_AndExceptionLoggedOnce()
        {
            var capturingProvider = new CapturingLoggerProvider();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(capturingProvider));

            var thrown = new InvalidOperationException("downstream boom");
            RequestDelegate terminal = _ => throw thrown;

            // Build the real pipeline order: RequestLogging wraps ExceptionHandling wraps the throwing terminal.
            var exceptionHandling = new ExceptionHandlingMiddleware(
                terminal,
                loggerFactory.CreateLogger<ExceptionHandlingMiddleware>(),
                CreateEnvironment(Environments.Production));
            var requestLogging = new RequestLoggingMiddleware(
                ctx => exceptionHandling.InvokeAsync(ctx),
                loggerFactory.CreateLogger<RequestLoggingMiddleware>());

            var context = CreateContextWithResponseBody();
            context.Request.Method = "GET";
            context.Request.Path = "/api/boom";
            var sessionService = new Game.Api.Services.SessionService(new NoOpSessionStore());

            // The exception is fully handled, so nothing escapes the request-logging middleware.
            await requestLogging.InvokeAsync(context, sessionService);

            Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

            var loggingCategory = typeof(RequestLoggingMiddleware).FullName!;
            var endedEntry = Assert.Single(
                capturingProvider.Entries.Where(e => e.Category == loggingCategory),
                e => e.Message.StartsWith("Request Ended"));
            var statusCode = (int)endedEntry.Properties.Single(p => p.Key == "StatusCode").Value!;
            Assert.Equal(500, statusCode);

            Assert.Single(capturingProvider.Entries, e => e.Category == LoggerCategory && e.Level == LogLevel.Error);
        }

        private static (CapturingLoggerProvider Provider, ILogger<ExceptionHandlingMiddleware> Logger) CreateLogger()
        {
            var provider = new CapturingLoggerProvider();
            var logger = new LoggerFactory([provider]).CreateLogger<ExceptionHandlingMiddleware>();
            return (provider, logger);
        }

        private static IReadOnlyList<CapturingEntry> MiddlewareEntries(CapturingLoggerProvider provider)
        {
            return provider.Entries.Where(e => e.Category == LoggerCategory).ToList();
        }

        private static IHostEnvironment CreateEnvironment(string environmentName)
        {
            return new FakeHostEnvironment { EnvironmentName = environmentName };
        }

        private static DefaultHttpContext CreateContextWithResponseBody()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            return context;
        }

        private static async Task<string?> ReadErrorMessage(HttpContext context)
        {
            context.Response.Body.Position = 0;
            var response = await JsonSerializer.DeserializeAsync<ApiResponse>(
                context.Response.Body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return response?.ErrorMessage;
        }

        private sealed class StartedResponseFeature : IHttpResponseFeature
        {
            public int StatusCode { get; set; } = StatusCodes.Status200OK;
            public string? ReasonPhrase { get; set; }
            public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
            public Stream Body { get; set; } = Stream.Null;
            public bool HasStarted => true;
            public void OnStarting(Func<object, Task> callback, object state) { }
            public void OnCompleted(Func<object, Task> callback, object state) { }
        }

        private sealed class FakeHostEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = Environments.Production;
            public string ApplicationName { get; set; } = nameof(ExceptionHandlingMiddlewareTests);
            public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
            public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
                new Microsoft.Extensions.FileProviders.NullFileProvider();
        }

        private sealed class NoOpSessionStore : Game.Abstractions.DataAccess.ISessionStore
        {
            public Task<Game.Core.Players.PlayerState?> GetSession(int userId, CancellationToken cancellationToken = default)
                => Task.FromResult<Game.Core.Players.PlayerState?>(null);
            public void Update(Game.Core.Players.PlayerState sessionData, int userId) { }
            public void Clear(int userId) { }
        }
    }
}
