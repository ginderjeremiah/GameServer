using Game.Abstractions.DataAccess;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Game.Api.Filters
{
    public class AdminCacheInvalidationFilter(IEnumerable<ICacheInvalidatable> caches) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executedContext = await next();
            if (executedContext.Exception is null || executedContext.ExceptionHandled)
            {
                foreach (var cache in caches)
                {
                    cache.InvalidateCache();
                }
            }
        }
    }
}
