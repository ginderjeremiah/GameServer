using Game.Application;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Game.Api.Filters
{
    /// <summary>
    /// An action filter that calls <see cref="IUnitOfWork.CommitAsync"/> after each successful
    /// controller action, persisting any changes queued in the EF Core change tracker.
    /// No commit is issued when the action throws an unhandled exception.
    /// </summary>
    public class CommitFilter(IUnitOfWork unitOfWork) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executedContext = await next();
            if (executedContext.Exception is null || executedContext.ExceptionHandled)
            {
                await unitOfWork.CommitAsync();
            }
        }
    }
}
