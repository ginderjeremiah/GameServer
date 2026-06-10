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
    /// previous snapshots until a sweep succeeds.
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
        /// </summary>
        public async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (await _signal.Reader.WaitToReadAsync(cancellationToken))
                {
                    await Task.Delay(policy.DebounceWindow, cancellationToken);
                    while (_signal.Reader.TryRead(out _))
                    {
                    }

                    await ReloadAllAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }

        private async Task ReloadAllAsync(CancellationToken cancellationToken)
        {
            for (var attempt = 1; attempt <= policy.MaxAttempts; attempt++)
            {
                try
                {
                    foreach (var cache in caches)
                    {
                        await cache.ReloadAsync(cancellationToken);
                    }

                    return;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < policy.MaxAttempts)
                {
                    logger.LogWarning(ex, "Background reference-cache reload failed on attempt {Attempt} of {MaxAttempts}; retrying.", attempt, policy.MaxAttempts);
                    await Task.Delay(policy.DelayAfterAttempt(attempt), cancellationToken);
                }
                catch (Exception ex)
                {
                    // Readers keep serving the previous snapshots; the next notification triggers a fresh sweep.
                    logger.LogError(ex, "Background reference-cache reload failed after {MaxAttempts} attempts; keeping the current snapshots.", policy.MaxAttempts);
                }
            }
        }
    }
}
