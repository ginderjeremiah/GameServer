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
                try
                {
                    await changeNotifier.NotifyChangedAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to broadcast the reference-data change; other instances may serve stale reference data until the next notification.");
                }

                foreach (var cache in caches)
                {
                    // Not tied to the request's cancellation token: the write has committed, so the cache
                    // must reflect it even if the client has disconnected.
                    await cache.ReloadAsync();
                }
            }
        }
    }
}
