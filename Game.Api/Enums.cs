namespace Game.Api
{
    public enum ESocketCloseReason
    {
        Finished = 0,
        Inactivity = 1,
        SocketReplaced = 2,
        MessageTooBig = 3,
        ServerShuttingDown = 4
    }

    /// <summary>
    /// The kind of business error an <see cref="Models.Common.IApiResponse"/> carries, mapped to an HTTP
    /// status code by <see cref="Filters.ErrorStatusFilter"/>. Lets a controller declare the error's
    /// semantics (auth, missing resource, validation) instead of every failure collapsing to a 400.
    /// </summary>
    public enum ApiErrorCategory
    {
        /// <summary>A validation or business-rule failure — the default, maps to 400 Bad Request.</summary>
        BadRequest = 0,
        /// <summary>The caller is not authenticated — maps to 401 Unauthorized.</summary>
        Unauthorized = 1,
        /// <summary>The requested resource does not exist — maps to 404 Not Found.</summary>
        NotFound = 2
    }
}
