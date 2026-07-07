using Game.Infrastructure.Redis;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Game.Infrastructure.Tests
{
    /// <summary>
    /// Unit tests for <see cref="RedisCommandBudget"/>, the helper that honours a per-command cancellation budget
    /// over StackExchange.Redis commands (which expose no token of their own). The behaviour that matters and is
    /// otherwise only exercised indirectly: a read throws up front on an already-cancelled budget without awaiting
    /// the command, and a write whose await is cancelled attaches a fault-logging continuation so an abandoned
    /// command that <em>later</em> faults is surfaced as an error rather than lost — while a late <em>success</em>
    /// logs nothing. The command is modelled with a <see cref="TaskCompletionSource{TResult}"/> so each transition
    /// (cancel, fault, succeed) is driven deterministically rather than raced against a real Redis round-trip.
    /// </summary>
    public class RedisCommandBudgetTests
    {
        [Fact]
        public async Task Read_WhenBudgetAlreadyCancelled_ThrowsWithoutAwaitingTheCommand()
        {
            // A command that never completes on its own: if Read awaited it the test would hang, so completing
            // promptly proves the already-cancelled budget is honoured up front (the racy-WaitAsync guard).
            var command = new TaskCompletionSource<int>();
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RedisCommandBudget.Read(command.Task, cts.Token));
            Assert.False(command.Task.IsCompleted);
        }

        [Fact]
        public async Task Read_AwaitCancelledThenCommandFaults_DoesNotSurfaceTheAbandonedFault()
        {
            var command = new TaskCompletionSource<int>();
            using var cts = new CancellationTokenSource();

            var read = RedisCommandBudget.Read(command.Task, cts.Token);

            // The await unwinds promptly on cancellation even though the command is still in flight.
            await cts.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);

            // The abandoned command later faults. The Read path attaches a silent fault-observing continuation, so
            // the exception is observed rather than left to surface via TaskScheduler.UnobservedTaskException — and
            // nothing escapes back to the already-unwound caller.
            command.SetException(new InvalidOperationException("redis blew up"));

            Assert.True(command.Task.IsFaulted);
            Assert.True(read.IsCanceled);
        }

        [Fact]
        public async Task Write_WhenBudgetAlreadyCancelled_ThrowsWithoutAwaitingTheCommand()
        {
            // A command that never completes on its own: if Write awaited it the test would hang, so completing
            // promptly proves the already-cancelled budget is honoured up front (the racy-WaitAsync guard).
            var command = new TaskCompletionSource<int>();
            using var cts = new CancellationTokenSource();
            var logger = new CapturingLogger();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RedisCommandBudget.Write(command.Task, cts.Token, logger, "ignored"));
            Assert.False(command.Task.IsCompleted);
            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task Write_Void_WhenBudgetAlreadyCancelled_ThrowsWithoutAwaitingTheCommand()
        {
            var command = new TaskCompletionSource();
            using var cts = new CancellationTokenSource();
            var logger = new CapturingLogger();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => RedisCommandBudget.Write(command.Task, cts.Token, logger, "ignored"));
            Assert.False(command.Task.IsCompleted);
            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task Write_AwaitCancelledThenCommandFaults_LogsTheAbandonedFault()
        {
            var command = new TaskCompletionSource<int>();
            using var cts = new CancellationTokenSource();
            var logger = new CapturingLogger();

            var write = RedisCommandBudget.Write(command.Task, cts.Token, logger, "the operation may not have been applied");

            // The await unwinds promptly on cancellation even though the command is still in flight.
            await cts.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write);

            // Nothing has faulted yet, so the continuation has logged nothing.
            Assert.Empty(logger.Entries);

            // The abandoned command settles with a fault; the continuation surfaces it as an error not a silent loss.
            command.SetException(new InvalidOperationException("redis blew up"));

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.NotNull(entry.Exception);
        }

        [Fact]
        public async Task Write_AwaitCancelledThenCommandSucceeds_LogsNothing()
        {
            var command = new TaskCompletionSource<int>();
            using var cts = new CancellationTokenSource();
            var logger = new CapturingLogger();

            var write = RedisCommandBudget.Write(command.Task, cts.Token, logger, "ignored");

            await cts.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => write);

            // The abandoned command completes successfully after the cancelled await; OnlyOnFaulted means the
            // continuation does not run, so a benign late success is not logged as a fault.
            command.SetResult(1);
            Assert.Empty(logger.Entries);
        }

        [Fact]
        public async Task Write_CommandSucceedsWithinBudget_ReturnsResultWithoutLogging()
        {
            var command = new TaskCompletionSource<int>();
            using var cts = new CancellationTokenSource();
            var logger = new CapturingLogger();

            command.SetResult(42);
            var result = await RedisCommandBudget.Write(command.Task, cts.Token, logger, "ignored");

            Assert.Equal(42, result);
            Assert.Empty(logger.Entries);
        }

        // A minimal capturing logger: the fault continuation's only observable effect is the error log, so the
        // level and whether an exception was carried are recorded. ExecuteSynchronously means a fault drives the
        // continuation inline on the SetException call, so the captured entry is visible without extra waiting.
        private sealed class CapturingLogger : ILogger
        {
            private readonly object _gate = new();
            private readonly List<(LogLevel Level, Exception? Exception)> _entries = [];

            public IReadOnlyList<(LogLevel Level, Exception? Exception)> Entries
            {
                get
                {
                    lock (_gate)
                    {
                        return _entries.ToList();
                    }
                }
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (_gate)
                {
                    _entries.Add((logLevel, exception));
                }
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose() { }
            }
        }
    }
}
