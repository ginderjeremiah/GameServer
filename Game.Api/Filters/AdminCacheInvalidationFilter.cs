using Game.Abstractions.DataAccess;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Game.Api.Filters
{
    public class AdminCacheInvalidationFilter(
        IEnemies enemies,
        IItems items,
        IItemMods itemMods,
        ISkills skills,
        IZones zones,
        IChallenges challenges) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executedContext = await next();
            if (executedContext.Exception is null || executedContext.ExceptionHandled)
            {
                enemies.InvalidateCache();
                items.InvalidateCache();
                itemMods.InvalidateCache();
                skills.InvalidateCache();
                zones.InvalidateCache();
                challenges.InvalidateCache();
            }
        }
    }
}
