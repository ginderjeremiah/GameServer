using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

namespace Game.Api.Filters
{
    /// <summary>
    /// Shared check for whether an action result carries an <see cref="IApiResponse"/> reporting an error.
    /// Used both to rewrite the HTTP status (<see cref="ErrorStatusFilter"/>) and to skip reference-cache
    /// reload/broadcast work for admin writes rejected before anything was staged
    /// (<see cref="AdminCacheReloadFilter"/>).
    /// </summary>
    public static class ApiResponseErrors
    {
        public static bool TryGetError(IActionResult? result, [NotNullWhen(true)] out IApiResponse? response)
        {
            if (result is ObjectResult objectResult
                && objectResult.Value is IApiResponse apiResponse
                && apiResponse.ErrorMessage is not null and not "")
            {
                response = apiResponse;
                return true;
            }

            response = null;
            return false;
        }
    }
}
