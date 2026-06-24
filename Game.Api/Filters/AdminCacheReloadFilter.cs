using Game.Abstractions.DataAccess;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Game.Api.Filters
{
    /// <summary>
    /// Reloads the in-memory reference caches after every successful admin write so the next read serves the
    /// freshly written data. Each cache rebuilds its snapshot off to the side and swaps it atomically, so
    /// players keep reading the previous snapshot with no gap while the admin request pays the reload cost —
    /// preserving the Workbench's read-your-writes guarantee. A reload failure after a successful write
    /// surfaces as an error on the admin response (the write persisted; the admin can retry). The write is
    /// also broadcast to every other API instance (<see cref="IReferenceDataChangeNotifier"/>), each of which
    /// reacts with a debounced background reload of its own caches (#359).
    /// </summary>
    /// <remarks>
    /// Apply via <see cref="ReloadReferenceCachesAttribute"/> rather than a bare
    /// <c>[ServiceFilter(typeof(AdminCacheReloadFilter))]</c>: the ordering below must hold for
    /// read-your-writes, and the attribute bakes it in so a controller cannot accidentally omit it.
    /// </remarks>
    public class AdminCacheReloadFilter(
        IEnumerable<IReloadableReferenceCache> caches,
        IReferenceDataChangeNotifier changeNotifier,
        ILogger<AdminCacheReloadFilter> logger) : IAsyncActionFilter
    {
        /// <summary>
        /// Ordered to run outermost among action filters so this filter's post-action reload executes AFTER
        /// the global <see cref="CommitFilter"/> has persisted the admin write. The reload queries the
        /// database on a fresh context, so it must observe the committed change; a higher (later-committing)
        /// order would reload before the write was visible and break read-your-writes.
        /// </summary>
        public const int FilterOrder = int.MinValue;

        /// <summary>
        /// Upper bound on the awaited local reload. The reload queries the database on a fresh context, so a
        /// wedged connection would otherwise hold the admin request open indefinitely (no request-token tie —
        /// see below). On timeout the reload is abandoned and a <see cref="TimeoutException"/> surfaces as an
        /// error on the admin response; the write persisted, so the admin can retry the reload by re-saving.
        /// </summary>
        private static readonly TimeSpan ReloadTimeout = TimeSpan.FromSeconds(30);

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executedContext = await next();
            if (executedContext.Exception is null || executedContext.ExceptionHandled)
            {
                // Broadcast first: the write is already committed at this point (this filter runs outside
                // CommitFilter), so other instances can begin their background reloads while this instance
                // pays its own awaited reload below. The broadcast is best-effort — a failure must never
                // abort the local read-your-writes reload, so it is swallowed with a warning (other
                // instances stay stale until the next notification, an accepted cost).
                //
                // Accepted asymmetry: because the broadcast runs *before* the local reload, a local-reload
                // failure (or timeout) after a successful broadcast leaves the serving instance as the *only*
                // stale one — the inverse of the intended read-your-writes guarantee — until its next admin
                // write or notification. Ordering it this way is deliberate (it overlaps the cross-instance
                // reloads with the local one); the window is small and self-heals on the next reload.
                try
                {
                    await changeNotifier.NotifyChangedAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to broadcast the reference-data change; other instances may serve stale reference data until the next notification.");
                }

                // Reload all holders concurrently rather than back-to-back: each rebuilds its snapshot on its
                // own DI-scoped context and swaps it atomically, and no holder depends on another's reload
                // order, so a serial pass only adds latency and could leave some sets fresh and some stale if
                // one query failed mid-list. Not tied to the request's cancellation token: the write has
                // committed, so the caches must reflect it even if the client has disconnected — but bounded by
                // ReloadTimeout so a wedged database connection can't hold the admin request open forever.
                await Task.WhenAll(caches.Select(cache => cache.ReloadAsync())).WaitAsync(ReloadTimeout);
            }
        }
    }
}
