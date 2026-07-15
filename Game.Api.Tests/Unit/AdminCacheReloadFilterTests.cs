using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
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
    /// a faulted action or a rejected write (an <see cref="IApiResponse"/> error result) skips both the
    /// broadcast and the reload entirely; and a wedged reload is cancelled and surfaced as a
    /// <see cref="TimeoutException"/> rather than orphaned.
    /// </summary>
    public class AdminCacheReloadFilterTests
    {
        private static ActionExecutingContext BuildExecutingContext()
        {
            var httpContext = new DefaultHttpContext();
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), controller: new object());
        }

        private static ActionExecutedContext BuildExecutedContext(
            Exception? exception, bool exceptionHandled, IActionResult? result = null)
        {
            var httpContext = new DefaultHttpContext();
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            return new ActionExecutedContext(actionContext, [], controller: new object())
            {
                Exception = exception!,
                ExceptionHandled = exceptionHandled,
                Result = result!,
            };
        }

        private static async Task<(int notified, int reloaded)> RunAsync(
            Exception? exception, bool exceptionHandled, bool broadcastThrows = false, IActionResult? result = null)
        {
            var cache = new RecordingCache();
            var notifier = new RecordingNotifier(broadcastThrows);
            var filter = new AdminCacheReloadFilter([cache], notifier, NullLogger<AdminCacheReloadFilter>.Instance);

            var executed = BuildExecutedContext(exception, exceptionHandled, result);
            await filter.OnActionExecutionAsync(BuildExecutingContext(), () => Task.FromResult(executed));

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

        [Fact]
        public async Task SkipsBroadcastAndReload_WhenTheWriteWasRejected()
        {
            // An AdminSaveResult.Failure-style rejection (e.g. "Enemy not found.") returns as a normal
            // ApiResponse error rather than an exception. Per the admin repos' contract, rejections happen
            // before anything is staged, so nothing changed and neither the broadcast nor the reload should run.
            var result = new ObjectResult(ApiResponse.Error("Enemy not found."));
            var (notified, reloaded) = await RunAsync(exception: null, exceptionHandled: false, result: result);
            Assert.Equal(0, notified);
            Assert.Equal(0, reloaded);
        }

        [Fact]
        public async Task BroadcastsAndReloads_WhenResultIsASuccessfulApiResponse()
        {
            // A success ApiResponse (no ErrorMessage) must not be mistaken for a rejection.
            var result = new ObjectResult(ApiResponse.Success());
            var (notified, reloaded) = await RunAsync(exception: null, exceptionHandled: false, result: result);
            Assert.Equal(1, notified);
            Assert.Equal(1, reloaded);
        }

        [Fact]
        public async Task PassesACancellableToken_ToEachReload()
        {
            // The reload must run under its own timeout-linked token (not CancellationToken.None) so a wedged
            // query can be cancelled; assert the filter hands the cache a token that can actually be cancelled.
            var cache = new RecordingCache();
            var filter = new AdminCacheReloadFilter([cache], new RecordingNotifier(throws: false), NullLogger<AdminCacheReloadFilter>.Instance);

            var executed = BuildExecutedContext(exception: null, exceptionHandled: false);
            await filter.OnActionExecutionAsync(BuildExecutingContext(), () => Task.FromResult(executed));

            Assert.True(cache.LastToken.CanBeCanceled);
        }

        [Fact]
        public async Task ThrowsTimeoutAndCancelsTheReload_WhenAReloadWedges()
        {
            // A reload that never completes on its own must be cancelled by the timeout and surface as a
            // TimeoutException — not hang or leave the query orphaned holding its gate.
            var cache = new WedgedCache();
            var filter = new AdminCacheReloadFilter(
                [cache], new RecordingNotifier(throws: false), NullLogger<AdminCacheReloadFilter>.Instance,
                reloadTimeout: TimeSpan.FromMilliseconds(50));

            var executed = BuildExecutedContext(exception: null, exceptionHandled: false);

            await Assert.ThrowsAsync<TimeoutException>(
                () => filter.OnActionExecutionAsync(BuildExecutingContext(), () => Task.FromResult(executed)));
            // The reload's token must actually be cancelled on timeout (so its query/gate are released) rather
            // than orphaned; awaiting the cancellation signal confirms it without racing on a plain flag.
            await cache.Cancelled.WaitAsync(TimeSpan.FromSeconds(5));
        }

        private sealed class RecordingCache : IReloadableReferenceCache
        {
            public int ReloadCount { get; private set; }
            public CancellationToken LastToken { get; private set; }

            public Task ReloadAsync(CancellationToken cancellationToken = default)
            {
                ReloadCount++;
                LastToken = cancellationToken;
                return Task.CompletedTask;
            }
        }

        private sealed class WedgedCache : IReloadableReferenceCache
        {
            private readonly TaskCompletionSource _cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);

            // Completes when the reload's token is cancelled — i.e. the wedged reload was released, not orphaned.
            public Task Cancelled => _cancelled.Task;

            public async Task ReloadAsync(CancellationToken cancellationToken = default)
            {
                using var registration = cancellationToken.Register(() => _cancelled.TrySetResult());
                await Task.Delay(Timeout.Infinite, cancellationToken);
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
