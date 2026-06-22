using Game.Api.Filters;
using Game.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Covers the commit gate: the filter commits the unit of work after a successful action (or one whose
    /// exception was handled), and deliberately skips the commit when the action threw an unhandled
    /// exception — so a faulted request never persists a partial change set.
    /// </summary>
    public class CommitFilterTests
    {
        // Runs the filter over an action that produced the given exception/handled state and returns how many
        // times CommitAsync was invoked.
        private static async Task<int> RunAsync(Exception? exception, bool exceptionHandled)
        {
            var unitOfWork = new RecordingUnitOfWork();
            var httpContext = new DefaultHttpContext();
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            var executing = new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), controller: new object());
            var executed = new ActionExecutedContext(actionContext, [], controller: new object())
            {
                Exception = exception!,
                ExceptionHandled = exceptionHandled,
            };

            await new CommitFilter(unitOfWork).OnActionExecutionAsync(executing, () => Task.FromResult(executed));

            return unitOfWork.CommitCount;
        }

        [Fact]
        public async Task Commits_WhenActionSucceeds()
        {
            Assert.Equal(1, await RunAsync(exception: null, exceptionHandled: false));
        }

        [Fact]
        public async Task Commits_WhenExceptionWasHandled()
        {
            // A handled exception means the action recovered, so the queued changes should still persist.
            Assert.Equal(1, await RunAsync(new InvalidOperationException("boom"), exceptionHandled: true));
        }

        [Fact]
        public async Task DoesNotCommit_WhenActionThrowsUnhandled()
        {
            // The faulted-request guard: an unhandled exception must leave the change set unpersisted.
            Assert.Equal(0, await RunAsync(new InvalidOperationException("boom"), exceptionHandled: false));
        }

        private sealed class RecordingUnitOfWork : IUnitOfWork
        {
            public int CommitCount { get; private set; }

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                CommitCount++;
                return Task.CompletedTask;
            }
        }
    }
}
