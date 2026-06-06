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
        /// <summary>
        /// The request header carrying the client-computed device fingerprint. Browsers don't send a
        /// device fingerprint on their own, so the frontend attaches it on every authenticated request.
        /// </summary>
        public const string DeviceFingerprintHeader = "X-Device-Fingerprint";

        /// <summary>Reads the user-agent and <c>Sec-CH-UA*</c> client-hint headers from the request.</summary>
        public static ClientHints FromHeaders(IHeaderDictionary headers) => new(
            headers.UserAgent.ToString(),
            NullIfEmpty(headers["Sec-CH-UA"].ToString()),
            NullIfEmpty(headers["Sec-CH-UA-Mobile"].ToString()),
            NullIfEmpty(headers["Sec-CH-UA-Platform"].ToString()));

        /// <summary>Reads the device fingerprint header, or null when the request did not carry one.</summary>
        public static string? DeviceFingerprint(IHeaderDictionary headers) =>
            NullIfEmpty(headers[DeviceFingerprintHeader].ToString());

        private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
