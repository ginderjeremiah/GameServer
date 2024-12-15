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
        /// <remarks>Attempts to determine if the endpoint result contains a standard error response and updates the status code accordingly.</remarks>
        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.HttpContext.Response.StatusCode == StatusCodes.Status200OK && HasError(context.Result))
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        /// <inheritdoc/>
        /// <remarks>Does nothing here.</remarks>
        public void OnResultExecuted(ResultExecutedContext context) { }

        /// <summary>
        /// Checks if the given <paramref name="result"/> contains a <see cref="IApiResponse"/> and it contains an <see cref="IApiResponse.ErrorMessage"/>.
        /// </summary>s
        /// <param name="result"></param>
        /// <returns>True if the <see cref="IActionResult"/> represents an <see cref="IApiResponse"/> with an error and false otherwise.</returns>
        private static bool HasError(IActionResult result)
        {
            return result is ObjectResult objectResult
                && objectResult.Value is IApiResponse response
                && response.ErrorMessage is not null or "";
        }
    }
}
