using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
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
        public void Start_PublishesIsRunningBeforeRunningTheAction(bool useAsync)
        {
            // Gate open so the action runs straight through. Start() publishes IsRunning = true *before* it signals
            // the worker loop (issue #238 item 2), so when the action runs it is guaranteed to observe the worker as
            // running. That happens-before is what stops a completed fast run from being left wedged 'true' by a
            // late write — were the flag published after signaling, the action could run (and the loop reset it to
            // false) before the 'true' landed.
            using var probe = new WorkerProbe(startReleased: true);
            var worker = CreateWorker(useAsync, probe);
            probe.RunningObserver = () => worker.IsRunning;

            worker.Start();

            AssertActionStarted(probe, "The action did not run after Start().");
            WaitUntil(() => probe.Invocations == 1);
            Assert.True(probe.ObservedRunningAtStart, "The action did not observe IsRunning == true when it ran.");

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Dispose_ReleasesResetEventHandle(bool useAsync)
        {
            using var probe = new WorkerProbe(startReleased: true);
            var worker = CreateWorker(useAsync, probe);

            // The fix this characterizes is the release of the AutoResetEvent's OS wait handle, which the class
            // previously left to the SafeWaitHandle finalizer. There is no public seam exposing it, so the disposal
            // is verified directly on the private field — the one deliberate white-box assertion in this suite.
            var handle = GetResetEvent(worker).SafeWaitHandle;
            Assert.False(handle.IsClosed);

            worker.Dispose();

            // Dispose() implies Kill(), whose Unregister(null) requests cancellation without blocking for the thread
            // pool to drop its reference on the handle. The SafeWaitHandle is reference-counted, so its count can
            // reach zero (closing the handle) a short, non-deterministic time after Dispose() returns — poll rather
            // than assert synchronously to absorb that window.
            WaitUntil(() => handle.IsClosed);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Dispose_PreventsFurtherStarts(bool useAsync)
        {
            using var probe = new WorkerProbe(startReleased: true);
            var worker = CreateWorker(useAsync, probe);

            worker.Dispose();

            // A disposed worker has released its wait handle, so starting it reports the disposed state.
            Assert.Throws<ObjectDisposedException>(worker.Start);
            Assert.Equal(0, probe.Invocations);
            Assert.False(worker.IsRunning);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Dispose_IsIdempotent(bool useAsync)
        {
            using var probe = new WorkerProbe(startReleased: true);
            var worker = CreateWorker(useAsync, probe);

            worker.Dispose();
            // Disposing again must be a no-op rather than throwing on the already-released handle.
            worker.Dispose();

            Assert.Throws<ObjectDisposedException>(worker.Start);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Dispose_AfterKill_DoesNotThrow(bool useAsync)
        {
            using var probe = new WorkerProbe(startReleased: true);
            var worker = CreateWorker(useAsync, probe);

            // Kill() and Dispose() are distinct steps; disposing an already-killed worker still releases the handle
            // and must not throw.
            var handle = GetResetEvent(worker).SafeWaitHandle;
            worker.Kill();
            worker.Dispose();

            // Unregister(null) does not block for the thread pool to drop its reference on the reference-counted
            // handle, so its closure settles asynchronously after Dispose() returns — poll rather than assert
            // synchronously to absorb that window.
            WaitUntil(() => handle.IsClosed);
            Assert.Throws<ObjectDisposedException>(worker.Start);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Dispose_DoesNotCancelAnInProgressAction(bool useAsync)
        {
            // Gate closed so the action is still in progress when Dispose() is called.
            using var probe = new WorkerProbe();
            var worker = CreateWorker(useAsync, probe);

            worker.Start();
            AssertActionStarted(probe, "The action did not start.");

            // Like Kill(), Dispose() must not abort the in-flight action: it stops future scheduling and releases
            // the handle, but the running delegate (which never touches the handle) is allowed to finish.
            worker.Dispose();
            probe.Release();

            WaitUntil(() => !worker.IsRunning);
            Assert.Equal(1, probe.Invocations);
            // The worker is disposed, so it cannot be started again.
            Assert.Throws<ObjectDisposedException>(worker.Start);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Start_AfterDispose_ThrowsCleanObjectDisposedExceptionForTheWorker(bool useAsync)
        {
            using var probe = new WorkerProbe(startReleased: true);
            var worker = CreateWorker(useAsync, probe);

            worker.Dispose();

            // The disposed guard must surface as an ObjectDisposedException naming the worker — not the internal
            // AutoResetEvent leaking its own disposed exception from a Set() on a released handle (issue #905).
            var ex = Assert.Throws<ObjectDisposedException>(worker.Start);
            Assert.Equal(typeof(BackgroundWorker).FullName, ex.ObjectName);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Start_RacingDispose_NeverLeaksTheResetEventDisposedException(bool useAsync)
        {
            // Stress the check-then-act window between Start()'s disposed guard and its _resetEvent.Set(): a Start()
            // that raced a full Dispose() previously resumed and called Set() on the released AutoResetEvent, leaking
            // an ObjectDisposedException naming the handle (issue #905). The fix serializes the two under a lock, so a
            // losing Start() must instead throw the clean worker-named guard. Several starter threads spin Start() in a
            // tight loop while one disposer disposes mid-flight, and the whole thing repeats many times — that crosses
            // the window reliably (it reproduced the leak on the pre-fix code). The assertion stays deterministic: any
            // ObjectDisposedException is allowed, but only if it names the worker, never the AutoResetEvent.
            const int starterThreads = 4;

            for (var iteration = 0; iteration < 100; iteration++)
            {
                using var probe = new WorkerProbe(startReleased: true);
                var worker = CreateWorker(useAsync, probe);
                using var ready = new Barrier(starterThreads + 1);

                var starters = new Task[starterThreads];
                for (var i = 0; i < starterThreads; i++)
                {
                    starters[i] = Task.Run(() =>
                    {
                        ready.SignalAndWait();
                        SpinStartUntilTornDown(worker);
                    });
                }

                var disposer = Task.Run(() =>
                {
                    ready.SignalAndWait();
                    worker.Dispose();
                });

                var race = Task.WhenAll([disposer, .. starters]);
                var finished = await Task.WhenAny(race, Task.Delay(WaitTimeout, TestContext.Current.CancellationToken));
                Assert.True(finished == race, "The race tasks did not complete in time.");
                // Re-await the completed race so a failed assertion on a starter thread propagates as the test failure.
                await race;
            }
        }

        [Fact]
        public void AsyncWorker_SignaledRepeatedlyDuringRun_NeverOverlapsAndSettlesNotRunning()
        {
            // Reproduces #1136: an async callback returns to the thread pool at its first incomplete await, which
            // re-arms the wait mid-run. Pre-fix, a Start() during an in-flight async run launched a *second concurrent*
            // loopAction and the finishing run's trailing IsRunning = false could clobber the new run's true. The fix
            // gates the async loop to one run at a time and coalesces mid-run signals into a single trailing pass.
            var active = 0;
            var maxActive = 0;
            var totalRuns = 0;
            using var firstRunStarted = new ManualResetEventSlim(false);
            var releaseFirstRun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task Action()
            {
                var now = Interlocked.Increment(ref active);
                InterlockedMax(ref maxActive, now);
                Interlocked.Increment(ref totalRuns);

                // Hold only the first run suspended mid-await so the test can fire more signals while it is in flight;
                // every later (coalesced) run runs straight through.
                if (!firstRunStarted.IsSet)
                {
                    firstRunStarted.Set();
                    await releaseFirstRun.Task.WaitAsync(WaitTimeout);
                }

                Interlocked.Decrement(ref active);
            }

            var worker = new BackgroundWorker(NullLogger<BackgroundWorker>.Instance, Action);

            worker.Start();
            Assert.True(firstRunStarted.Wait(WaitTimeout, TestContext.Current.CancellationToken), "The first async run did not start.");
            Assert.True(worker.IsRunning);

            // Signal repeatedly while the first run is suspended at its await. Pre-fix each re-armed wait fired a
            // concurrent run (driving maxActive past 1); post-fix they collapse into one trailing pass.
            for (var i = 0; i < 20; i++)
            {
                worker.Start();
            }

            releaseFirstRun.SetResult();
            // The mid-run signals must produce at least one trailing run — coalesced into the in-flight run, or
            // dispatched as a fresh run once it exits — and the worker must then settle for good. Wait for a *stable*
            // non-running terminal state with that trailing run observed: sampling once could otherwise land in the
            // brief gap after the first run exits but before the coalesced/leftover pass runs, reading totalRuns == 1
            // or a transient IsRunning.
            WaitUntilStablyNotRunning(worker, () => Volatile.Read(ref active) == 0 && Volatile.Read(ref totalRuns) >= 2);

            // Two loopAction calls never ran at once.
            Assert.Equal(1, Volatile.Read(ref maxActive));
            // The mid-run signals were not lost — the coalesced trailing run executed.
            Assert.True(Volatile.Read(ref totalRuns) >= 2, "The coalesced trailing run did not execute.");
            // The worker settles in a stable, non-running terminal state.
            Assert.False(worker.IsRunning);

            worker.Kill();
        }

        [Fact]
        public async Task SyncWorker_StartRacingRunCompletion_NeverObservesNotRunningAtRunStart()
        {
            // Reproduces #1822: pre-fix, the sync loop had no run-serialization gate, so a Start() landing while a
            // run was still executing could dispatch a genuinely concurrent second run whose IsRunning = true got
            // clobbered by the finishing run's trailing IsRunning = false — leaving that run observing IsRunning
            // false throughout. The fix routes the sync overload through the same gated loop the async overload
            // uses. Several threads spin Start() in a tight loop against a fast, non-blocking run to cross that
            // window many times.
            const int starterThreads = 4;
            const int minInvocations = 3000;

            using var probe = new WorkerProbe(startReleased: true);
            var worker = new BackgroundWorker(NullLogger<BackgroundWorker>.Instance, probe.SyncAction);
            probe.RunningObserver = () => worker.IsRunning;

            var stop = 0;
            var starters = new Task[starterThreads];
            for (var i = 0; i < starterThreads; i++)
            {
                starters[i] = Task.Run(() =>
                {
                    while (Volatile.Read(ref stop) == 0)
                    {
                        try
                        {
                            worker.Start();
                        }
                        catch (InvalidOperationException)
                        {
                            // A losing Start() racing Kill()/Dispose() at teardown is a legitimate outcome, not
                            // the race under test.
                            return;
                        }
                    }
                });
            }

            WaitUntil(() => probe.Invocations >= minInvocations);
            Volatile.Write(ref stop, 1);
            var race = Task.WhenAll(starters);
            var finished = await Task.WhenAny(race, Task.Delay(WaitTimeout, TestContext.Current.CancellationToken));
            Assert.True(finished == race, "The starter threads did not stop in time.");
            await race;

            worker.Kill();

            Assert.Equal(0, probe.NotRunningAtStartCount);
        }

        // Atomically raises target to value when value is greater, used to detect any overlapping run.
        private static void InterlockedMax(ref int target, int value)
        {
            int current;
            do
            {
                current = Volatile.Read(ref target);
                if (value <= current)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref target, value, current) != current);
        }

        // Calls Start() repeatedly until the worker is torn down, asserting any disposed exception is the clean
        // worker-named guard rather than a leaked AutoResetEvent disposal (the issue #905 race).
        private static void SpinStartUntilTornDown(BackgroundWorker worker)
        {
            while (true)
            {
                try
                {
                    worker.Start();
                }
                catch (ObjectDisposedException ex)
                {
                    Assert.Equal(typeof(BackgroundWorker).FullName, ex.ObjectName);
                    return;
                }
                catch (InvalidOperationException)
                {
                    // Dispose() implies Kill(); observing the killed state is a legitimate teardown outcome.
                    return;
                }
            }
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

        private static AutoResetEvent GetResetEvent(BackgroundWorker worker)
        {
            var field = typeof(BackgroundWorker).GetField("_resetEvent", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            return Assert.IsType<AutoResetEvent>(field.GetValue(worker));
        }

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

        // Waits until the worker is not running and <paramref name="alsoRequire"/> holds, and that combined state
        // stays stable across several consecutive polls. The async loop re-arms its wait mid-run, so the burst of
        // signals can leave the AutoResetEvent with a leftover pass that dispatches a moment after the worker first
        // looks idle; requiring a run of stable observations rules out sampling that transient between-runs gap.
        private static void WaitUntilStablyNotRunning(BackgroundWorker worker, Func<bool> alsoRequire)
        {
            const int requiredStablePolls = 10;
            var deadline = DateTime.UtcNow + WaitTimeout;
            var stablePolls = 0;
            while (DateTime.UtcNow < deadline)
            {
                if (!worker.IsRunning && alsoRequire())
                {
                    if (++stablePolls >= requiredStablePolls)
                    {
                        return;
                    }
                }
                else
                {
                    stablePolls = 0;
                }

                Thread.Sleep(5);
            }

            Assert.Fail("The worker did not settle into a stable non-running state with the coalesced trailing run executed.");
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
            private int _notRunningAtStartCount;

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

            /// <summary>
            /// Optional callback invoked at the very start of each run to capture worker state the action can see at
            /// that instant (used to verify IsRunning is published before the worker loop is signaled).
            /// </summary>
            public Func<bool>? RunningObserver { get; set; }

            /// <summary>The value <see cref="RunningObserver"/> returned on the most recent run start.</summary>
            public bool ObservedRunningAtStart { get; private set; }

            /// <summary>The number of runs across the probe's lifetime that observed <see cref="RunningObserver"/> false at start.</summary>
            public int NotRunningAtStartCount => Volatile.Read(ref _notRunningAtStartCount);

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
                // Capture observed state before the Interlocked.Increment so its release semantics publish the write
                // to any thread that subsequently observes the incremented invocation count.
                if (RunningObserver is not null)
                {
                    ObservedRunningAtStart = RunningObserver();
                    if (!ObservedRunningAtStart)
                    {
                        Interlocked.Increment(ref _notRunningAtStartCount);
                    }
                }
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
