using Game.Abstractions.DataAccess;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Game.DataAccess
{
    /// <summary>
    /// Turns "reference data changed" signals into background reload sweeps over every
    /// <see cref="IReloadableReferenceCache"/>, coalescing bursts so a Workbench save (several admin writes
    /// in quick succession) costs one sweep rather than one per write. The pending signal is a single flag
    /// rather than a queue: any number of signals raised while a sweep is pending or running collapse into
    /// at most one follow-up sweep, which (on top of the per-holder single-flight reload gate) keeps the
    /// caches eventually consistent without re-reloading once per notification. A failed sweep is retried
    /// with exponential backoff and never throws out of the loop — readers keep serving the holders'
    /// previous snapshots until a sweep succeeds. A periodic reconciliation sweep additionally runs after
    /// <see cref="ReferenceCacheReloadPolicy.ReconciliationInterval"/> of signal silence, self-healing a
    /// notification this instance never received (Redis pub/sub delivery is at-most-once).
    /// </summary>
    internal sealed class CoalescingReferenceCacheReloader(
        IEnumerable<IReloadableReferenceCache> caches,
        ReferenceCacheReloadPolicy policy,
        ILogger<CoalescingReferenceCacheReloader> logger)
    {
        // A capacity-1 drop-on-full channel is the "dirty" flag and the wakeup signal in one: writes never
        // block (signaled from the pub/sub callback thread) and any number of signals collapse to one item.
        private readonly Channel<bool> _signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
        });

        /// <summary>Signals that reference data changed; non-blocking and safe to call from any thread.</summary>
        public void NotifyChanged()
        {
            _signal.Writer.TryWrite(true);
        }

        /// <summary>
        /// Processes signals until cancelled. Each iteration waits out the debounce window so an in-flight
        /// burst lands as one sweep, drains every signal the burst produced, then reloads all caches. The
        /// drain happens before the reload — never after — so a signal that arrives mid-sweep survives to
        /// trigger a follow-up sweep and a change can never slip between the drain and the reload.
        /// <para>
        /// If <see cref="ReferenceCacheReloadPolicy.ReconciliationInterval"/> elapses with no signal at all,
        /// a sweep runs anyway — a reconciliation backstop for Redis pub/sub's at-most-once delivery, which
        /// otherwise leaves an instance that missed a notification (e.g. mid-reconnect) stale indefinitely.
        /// </para>
        /// </summary>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    var reconciling = await WaitForNextSweepAsync(cancellationToken);
                    if (!reconciling)
                    {
                        await Task.Delay(policy.DebounceWindow, cancellationToken);
                    }

                    while (_signal.Reader.TryRead(out _))
                    {
                    }

                    if (reconciling)
                    {
                        // Routine idle-steady-state activity (no admin write happened anywhere in the cluster
                        // in this window) rather than evidence of a missed signal, so this logs at Debug, not
                        // Information — an operator skimming logs shouldn't read it as a delivery problem.
                        logger.LogDebug(
                            "Periodic reference-cache reconciliation sweep running ({Interval} since the last sweep).",
                            policy.ReconciliationInterval);
                    }

                    await ReloadAllAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }

        /// <summary>
        /// Waits for either a change signal or the reconciliation interval, whichever comes first. Returns
        /// <see langword="true"/> if the interval elapsed with no signal (a reconciliation sweep is due), or
        /// <see langword="false"/> if a signal arrived first (the ordinary debounce-then-reload path). A
        /// <see langword="null"/> <see cref="ReferenceCacheReloadPolicy.ReconciliationInterval"/> makes the
        /// periodic wait never elapse, so this degrades to signal-only waiting.
        /// </summary>
        private async Task<bool> WaitForNextSweepAsync(CancellationToken cancellationToken)
        {
            using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var signalWait = _signal.Reader.WaitToReadAsync(timerCts.Token).AsTask();
            var periodicWait = Task.Delay(policy.ReconciliationInterval ?? Timeout.InfiniteTimeSpan, timerCts.Token);

            var reconciling = await Task.WhenAny(signalWait, periodicWait) == periodicWait;
            // Cancel whichever wait didn't win so it doesn't keep running into the next iteration.
            timerCts.Cancel();

            // Observe both tasks so cancelling the loser never surfaces as an unobserved task exception. This
            // also catches the case where cancellationToken itself (not the interval) fired mid-wait — either
            // task can be the one that happens to have observed it — so re-check it explicitly afterward
            // rather than trusting which task "won" the race above.
            await ObserveCancellationAsync(signalWait);
            await ObserveCancellationAsync(periodicWait);
            cancellationToken.ThrowIfCancellationRequested();

            return reconciling;
        }

        /// <summary>Awaits a task expected to be cancelled by the losing side of a <see cref="Task.WhenAny(Task[])"/> race, swallowing the resulting cancellation so it never surfaces as an unobserved task exception.</summary>
        private static async Task ObserveCancellationAsync(Task task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // Expected: this side lost the race and was cancelled via the shared linked token.
            }
        }

        private async Task ReloadAllAsync(CancellationToken cancellationToken)
        {
            var pending = caches.ToList();
            for (var attempt = 1; attempt <= policy.MaxAttempts; attempt++)
            {
                // Retry only the holders that haven't succeeded yet: each reload is a build-then-swap that
                // leaves the prior snapshot in place on failure, so re-reloading a succeeded holder is wasted work.
                var failure = await ReloadConcurrentlyAsync(pending, cancellationToken);
                if (failure is null)
                {
                    return;
                }

                pending = failure.StillPending;
                if (attempt < policy.MaxAttempts)
                {
                    logger.LogWarning(failure.Error, "Background reference-cache reload failed on attempt {Attempt} of {MaxAttempts}; retrying.", attempt, policy.MaxAttempts);
                    await Task.Delay(policy.DelayAfterAttempt(attempt), cancellationToken);
                }
                else
                {
                    // Readers keep serving the previous snapshots; the next notification triggers a fresh sweep.
                    logger.LogError(failure.Error, "Background reference-cache reload failed after {MaxAttempts} attempts; keeping the current snapshots.", policy.MaxAttempts);
                }
            }
        }

        /// <summary>
        /// Reloads every still-pending holder concurrently — matching the request-time AdminCacheReloadFilter:
        /// each rebuilds its snapshot on its own scoped context and swaps it atomically, and no holder depends
        /// on another's reload order, so a serial pass only adds latency and could leave some sets fresh and
        /// some stale if one query failed mid-list. Awaits every reload before inspecting results so one
        /// failure doesn't abandon the others mid-flight, returns <c>null</c> on full success, and rethrows
        /// promptly on cancellation so the loop unwinds.
        /// </summary>
        private static async Task<ReloadFailure?> ReloadConcurrentlyAsync(
            List<IReloadableReferenceCache> pending,
            CancellationToken cancellationToken)
        {
            // Start every reload, capturing a holder that throws synchronously as a faulted task so it lines up
            // with the others by index and is retried like any async failure (rather than escaping the await).
            var reloads = pending.Select(cache => StartReload(cache, cancellationToken)).ToList();
            try
            {
                await Task.WhenAll(reloads);
                return null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                // Keep the holders whose reload didn't succeed; the succeeded ones drop out so a retry skips
                // them. Task.WhenAll surfaces only the first fault, which is enough to log the failure cause.
                var stillPending = new List<IReloadableReferenceCache>();
                for (var i = 0; i < pending.Count; i++)
                {
                    if (!reloads[i].IsCompletedSuccessfully)
                    {
                        stillPending.Add(pending[i]);
                    }
                }

                return new ReloadFailure(stillPending, error);
            }
        }

        /// <summary>Invokes a reload, turning a synchronous throw into a faulted task so the caller can await it uniformly.</summary>
        private static Task StartReload(IReloadableReferenceCache cache, CancellationToken cancellationToken)
        {
            try
            {
                return cache.ReloadAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        /// <summary>A failed concurrent sweep: the holders still needing a reload and the fault to log.</summary>
        private sealed record ReloadFailure(List<IReloadableReferenceCache> StillPending, Exception Error);
    }
}
