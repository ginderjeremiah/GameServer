using Game.Abstractions.DataAccess;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Game.Api.Filters
{
    /// <summary>
    /// Reloads the in-memory reference caches after every successful admin write so the next read serves the
    /// freshly written data. Each cache rebuilds its snapshot off to the side and swaps it atomically, so
    /// players keep reading the previous snapshot with no gap while the admin request pays the reload cost —
    /// preserving the Workbench's read-your-writes guarantee. A reload failure after a successful write
    /// surfaces as an error on the admin response (the write persisted; the admin can retry).
    /// </summary>
    public class AdminCacheInvalidationFilter(IEnumerable<IReloadableReferenceCache> caches) : IAsyncActionFilter
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
