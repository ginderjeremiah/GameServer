using Game.Api.Filters;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Covers the 200→400 rewrite the whole error contract depends on: a success-status response
    /// carrying an <see cref="IApiResponse"/> error is rewritten to 400, while every other shape
    /// (no error, non-response value, non-object result, already-non-200 status) is left untouched.
    /// </summary>
    public class ErrorStatusFilterTests
    {
        // Runs the filter over the given result at the given starting status and returns the resulting code.
        private static int RunFilter(IActionResult result, int startingStatusCode = StatusCodes.Status200OK)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Response.StatusCode = startingStatusCode;
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            var context = new ResultExecutingContext(actionContext, [], result, controller: new object());

            new ErrorStatusFilter().OnResultExecuting(context);

            return httpContext.Response.StatusCode;
        }

        [Fact]
        public void RewritesTo400_WhenResponseCarriesAnError()
        {
            var result = new ObjectResult(ApiResponse.Error("Something went wrong."));

            Assert.Equal(StatusCodes.Status400BadRequest, RunFilter(result));
        }

        [Fact]
        public void LeavesStatus_WhenResponseHasNoError()
        {
            var result = new ObjectResult(ApiResponse.Success());

            Assert.Equal(StatusCodes.Status200OK, RunFilter(result));
        }

        [Fact]
        public void LeavesStatus_WhenResponseErrorIsEmptyString()
        {
            var result = new ObjectResult(new ApiResponse { ErrorMessage = "" });

            Assert.Equal(StatusCodes.Status200OK, RunFilter(result));
        }

        [Fact]
        public void LeavesStatus_WhenObjectResultValueIsNotAnApiResponse()
        {
            var result = new ObjectResult("just a string");

            Assert.Equal(StatusCodes.Status200OK, RunFilter(result));
        }

        [Fact]
        public void LeavesStatus_WhenResultIsNotAnObjectResult()
        {
            // EmptyResult is not an ObjectResult, so HasError can never inspect a value.
            Assert.Equal(StatusCodes.Status200OK, RunFilter(new EmptyResult()));
        }

        [Fact]
        public void DoesNotRewrite_WhenStartingStatusIsNot200()
        {
            // The rewrite is gated on a 200 status; an error response already returned with a non-200
            // status (e.g. a controller that set 201) is left exactly as-is.
            var result = new ObjectResult(ApiResponse.Error("boom"));

            Assert.Equal(StatusCodes.Status201Created, RunFilter(result, StatusCodes.Status201Created));
        }
    }
}
