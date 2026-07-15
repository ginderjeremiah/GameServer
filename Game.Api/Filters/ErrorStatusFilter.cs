using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Game.Api.Filters
{
    /// <summary>
    /// An MVC endpoint filter that will check for errors in response messages and update HTTP status code to match.
    /// </summary>
    public class ErrorStatusFilter : IResultFilter
    {
        /// <inheritdoc/>
        /// <remarks>
        /// Rewrites a success-status response carrying an <see cref="IApiResponse"/> error to the status
        /// implied by its <see cref="IApiResponse.ErrorCategory"/> (defaulting to 400), preserving the
        /// distinct HTTP semantics of auth (401) and missing-resource (404) failures.
        /// </remarks>
        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.HttpContext.Response.StatusCode == StatusCodes.Status200OK
                && ApiResponseErrors.TryGetError(context.Result, out var response))
            {
                context.HttpContext.Response.StatusCode = StatusForCategory(response.ErrorCategory);
            }
        }

        /// <inheritdoc/>
        /// <remarks>Does nothing here.</remarks>
        public void OnResultExecuted(ResultExecutedContext context) { }

        // Maps an error category to its HTTP status. An unrecognised category defaults to 400 so a newly
        // added category fails safe rather than passing the failure through as a 200.
        private static int StatusForCategory(ApiErrorCategory category)
        {
            return category switch
            {
                ApiErrorCategory.Unauthorized => StatusCodes.Status401Unauthorized,
                ApiErrorCategory.NotFound => StatusCodes.Status404NotFound,
                ApiErrorCategory.TooManyRequests => StatusCodes.Status429TooManyRequests,
                _ => StatusCodes.Status400BadRequest,
            };
        }
    }
}
