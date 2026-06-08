using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Game.Infrastructure.Tests
{
    /// <summary>
    /// Lifecycle tests for the <see cref="BackgroundWorker"/> Start/Kill state machine.
    /// <para>
    /// <see cref="BackgroundWorker"/> runs its delegate on a thread-pool thread via
    /// <c>ThreadPool.RegisterWaitForSingleObject</c>, so the tests synchronize on signals the action
    /// itself raises (<see cref="WorkerProbe.Started"/>, the release gate) rather than on sleeps, keeping
    /// them deterministic instead of timing-flaky. Following the project's classical testing guidance
    /// (<c>docs/backend.md</c>), they assert observable behaviour — invocation, <see cref="BackgroundWorker.IsRunning"/>,
    /// exception non-propagation — using a real <see cref="NullLogger{T}"/> rather than spying on log calls.
    /// </para>
    /// </summary>
    public class BackgroundWorkerTests
    {
        // Generous upper bound on thread-pool scheduling. Tests return as soon as their condition holds,
        // so this only ever bounds a genuine hang/failure.
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Construction_DoesNotInvokeActionAndIsNotRunning(bool useAsync)
        {
            using var probe = new WorkerProbe();
            var worker = CreateWorker(useAsync, probe);

            Assert.False(worker.IsRunning);
            Assert.Equal(0, probe.Invocations);
            Assert.False(probe.Started.IsSet);

            worker.Kill();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Start_InvokesAction(bool useAsync)
        {
            using var probe = new WorkerProbe(startReleased: true);
            var worker = CreateWorker(useAsync, probe);

            worker.Start();

            AssertActionStarted(probe, "The action did not run after Start().");
            WaitUntil(() => probe.Invocations == 1);

            worker.Kill();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Start_SetsIsRunningWhileActionRuns_ThenResetsOnCompletion(bool useAsync)
        {
            // Gate closed: the action blocks part-way through so we can observe the running state.
            using var probe = new WorkerProbe();
            var worker = CreateWorker(useAsync, probe);

            worker.Start();

            AssertActionStarted(probe, "The action did not start.");
            // The action is suspended mid-run, so the worker must report itself as running.
            Assert.True(worker.IsRunning);

            probe.Release();
            // Once the action completes, the worker loop resets IsRunning back to false.
            WaitUntil(() => !worker.IsRunning);

            worker.Kill();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Start_AfterCompletion_InvokesActionAgain(bool useAsync)
        {
            using var probe = new WorkerProbe(startReleased: true);
            var worker = CreateWorker(useAsync, probe);

            worker.Start();
            AssertActionStarted(probe, "The first run did not start.");
            WaitUntil(() => !worker.IsRunning);

            probe.ResetStarted();
            worker.Start();
            AssertActionStarted(probe, "The second run did not start.");
            WaitUntil(() => probe.Invocations == 2 && !worker.IsRunning);

            worker.Kill();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Action_WhenItThrows_SwallowsExceptionAndRemainsUsable(bool useAsync)
        {
            using var probe = new WorkerProbe(startReleased: true) { ShouldThrow = true };
            var worker = CreateWorker(useAsync, probe);

            // The action throws inside the worker loop on its first run.
            worker.Start();
            AssertActionStarted(probe, "The throwing action did not run.");
            // The exception is swallowed (never reaches this thread / crashes the process) and the loop
            // still resets IsRunning, so the worker is not left wedged in the running state.
            WaitUntil(() => !worker.IsRunning);
            Assert.Equal(1, probe.Invocations);

            // The thrown exception must not poison the worker: a subsequent run executes normally.
            probe.ShouldThrow = false;
            probe.ResetStarted();
            worker.Start();
            AssertActionStarted(probe, "The worker did not run again after an exception.");
            WaitUntil(() => probe.Invocations == 2 && !worker.IsRunning);

            worker.Kill();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Start_AfterKill_ThrowsInvalidOperationException(bool useAsync)
        {
            using var probe = new WorkerProbe(startReleased: true);
            var worker = CreateWorker(useAsync, probe);

            worker.Kill();

            Assert.Throws<InvalidOperationException>(worker.Start);
            // A killed worker never queues the action and stays in the non-running state.
            Assert.Equal(0, probe.Invocations);
            Assert.False(worker.IsRunning);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Kill_DoesNotCancelAnInProgressAction(bool useAsync)
        {
            // Gate closed so the action is still in progress when Kill() is called.
            using var probe = new WorkerProbe();
            var worker = CreateWorker(useAsync, probe);

            worker.Start();
            AssertActionStarted(probe, "The action did not start.");

            // Kill() only prevents future runs; per its contract it must not abort the in-flight action.
            worker.Kill();
            probe.Release();

            WaitUntil(() => !worker.IsRunning);
            Assert.Equal(1, probe.Invocations);
            // The worker is killed, so it cannot be started again.
            Assert.Throws<InvalidOperationException>(worker.Start);
        }

        private static BackgroundWorker CreateWorker(bool useAsync, WorkerProbe probe)
        {
            var logger = NullLogger<BackgroundWorker>.Instance;
            return useAsync
                ? new BackgroundWorker(logger, probe.AsyncAction)
                : new BackgroundWorker(logger, probe.SyncAction);
        }

        private static void AssertActionStarted(WorkerProbe probe, string because) =>
            Assert.True(probe.Started.Wait(WaitTimeout, TestContext.Current.CancellationToken), because);

        private static void WaitUntil(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow + WaitTimeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                {
                    return;
                }

                Thread.Sleep(5);
            }

            Assert.True(condition(), "The expected condition was not met within the timeout.");
        }

        /// <summary>
        /// A controllable delegate for driving a <see cref="BackgroundWorker"/> deterministically. It exposes
        /// equivalent synchronous (<see cref="SyncAction"/>) and asynchronous (<see cref="AsyncAction"/>) forms
        /// so the same lifecycle assertions can be run against both constructor overloads. The action signals
        /// <see cref="Started"/> when it begins and blocks on a release gate so a test can hold it mid-run.
        /// </summary>
        private sealed class WorkerProbe : IDisposable
        {
            private readonly ManualResetEventSlim _release;
            private readonly TaskCompletionSource _releaseTask =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _invocations;

            /// <param name="startReleased">
            /// When true the gate starts open, so the action runs straight through instead of blocking.
            /// </param>
            public WorkerProbe(bool startReleased = false)
            {
                _release = new ManualResetEventSlim(startReleased);
                if (startReleased)
                {
                    _releaseTask.SetResult();
                }
            }

            /// <summary>Signaled each time the action begins executing.</summary>
            public ManualResetEventSlim Started { get; } = new(false);

            /// <summary>The number of times the action has begun executing.</summary>
            public int Invocations => Volatile.Read(ref _invocations);

            /// <summary>When true, the action throws after starting, exercising the loop's swallow path.</summary>
            public bool ShouldThrow { get; set; }

            public Action SyncAction => () =>
            {
                Begin();
                _release.Wait(WaitTimeout);
                Finish();
            };

            public Func<Task> AsyncAction => async () =>
            {
                Begin();
                await _releaseTask.Task.WaitAsync(WaitTimeout);
                Finish();
            };

            /// <summary>Opens the gate so a blocked (or subsequent) invocation runs to completion.</summary>
            public void Release()
            {
                _release.Set();
                _releaseTask.TrySetResult();
            }

            /// <summary>Clears the per-run started signal so the next invocation can be awaited.</summary>
            public void ResetStarted() => Started.Reset();

            public void Dispose()
            {
                _release.Dispose();
                Started.Dispose();
            }

            private void Begin()
            {
                Interlocked.Increment(ref _invocations);
                Started.Set();
            }

            private void Finish()
            {
                if (ShouldThrow)
                {
                    throw new InvalidOperationException("Simulated action failure.");
                }
            }
        }
    }
}
