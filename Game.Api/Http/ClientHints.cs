namespace Game.Api.Http
{
    /// <summary>
    /// The user-agent string and low-entropy client-hint headers carried on an HTTP request, used to
    /// identify the requesting browser/device for connection tracking.
    /// </summary>
    public readonly record struct ClientHints(
        string UserAgent,
        string? SecChUa,
        string? SecChUaMobile,
        string? SecChUaPlatform)
    {
        /// <summary>Reads the user-agent and <c>Sec-CH-UA*</c> client-hint headers from the request.</summary>
        public static ClientHints FromHeaders(IHeaderDictionary headers) => new(
            headers.UserAgent.ToString(),
            NullIfEmpty(headers["Sec-CH-UA"].ToString()),
            NullIfEmpty(headers["Sec-CH-UA-Mobile"].ToString()),
            NullIfEmpty(headers["Sec-CH-UA-Platform"].ToString()));

        private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
