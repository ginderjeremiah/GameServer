using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Covers the post-admin-write reload gate: on a successful write the filter broadcasts the change and
    /// reloads every cache; a broadcast failure is swallowed so the local read-your-writes reload still runs;
    /// and a faulted action skips both the broadcast and the reload entirely.
    /// </summary>
    public class AdminCacheReloadFilterTests
    {
        private static async Task<(int notified, int reloaded)> RunAsync(
            Exception? exception, bool exceptionHandled, bool broadcastThrows = false)
        {
            var cache = new RecordingCache();
            var notifier = new RecordingNotifier(broadcastThrows);
            var filter = new AdminCacheReloadFilter([cache], notifier, NullLogger<AdminCacheReloadFilter>.Instance);

            var httpContext = new DefaultHttpContext();
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            var executing = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), controller: new object());
            var executed = new ActionExecutedContext(actionContext, [], controller: new object())
            {
                Exception = exception!,
                ExceptionHandled = exceptionHandled,
            };

            await filter.OnActionExecutionAsync(executing, () => Task.FromResult(executed));

            return (notifier.NotifyCount, cache.ReloadCount);
        }

        [Fact]
        public async Task BroadcastsAndReloads_WhenWriteSucceeds()
        {
            var (notified, reloaded) = await RunAsync(exception: null, exceptionHandled: false);
            Assert.Equal(1, notified);
            Assert.Equal(1, reloaded);
        }

        [Fact]
        public async Task StillReloads_WhenBroadcastThrows()
        {
            // A best-effort broadcast failure must be swallowed so the local read-your-writes reload still runs.
            var (notified, reloaded) = await RunAsync(exception: null, exceptionHandled: false, broadcastThrows: true);
            Assert.Equal(1, notified);
            Assert.Equal(1, reloaded);
        }

        [Fact]
        public async Task SkipsBroadcastAndReload_WhenActionThrowsUnhandled()
        {
            // A faulted admin write hasn't committed, so neither the broadcast nor the reload should run.
            var (notified, reloaded) = await RunAsync(new InvalidOperationException("boom"), exceptionHandled: false);
            Assert.Equal(0, notified);
            Assert.Equal(0, reloaded);
        }

        private sealed class RecordingCache : IReloadableReferenceCache
        {
            public int ReloadCount { get; private set; }

            public Task ReloadAsync(CancellationToken cancellationToken = default)
            {
                ReloadCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingNotifier(bool throws) : IReferenceDataChangeNotifier
        {
            public int NotifyCount { get; private set; }

            public Task NotifyChangedAsync()
            {
                NotifyCount++;
                if (throws)
                {
                    throw new InvalidOperationException("backplane down");
                }
                return Task.CompletedTask;
            }
        }
    }
}
