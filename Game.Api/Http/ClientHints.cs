using System.Text.RegularExpressions;

namespace Game.Api.Http
{
    /// <summary>
    /// The user-agent string and low-entropy client-hint headers carried on an HTTP request, used to
    /// identify the requesting browser/device for connection tracking.
    /// </summary>
    public readonly partial record struct ClientHints(
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

        /// <summary>
        /// Reads the device fingerprint header, or null when the request did not carry one or its value
        /// doesn't have the shape of the frontend's SHA-256 hex digest (<see cref="FingerprintShapeRegex"/>).
        /// Rejecting anything else keeps an arbitrary client-supplied string from reaching the tracking
        /// tables (#2064) — the header is otherwise unauthenticated client input.
        /// </summary>
        public static string? DeviceFingerprint(IHeaderDictionary headers)
        {
            var value = NullIfEmpty(headers[DeviceFingerprintHeader].ToString());
            return value is not null && FingerprintShapeRegex().IsMatch(value) ? value : null;
        }

        private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

        // A SHA-256 digest rendered as lowercase hex (device-fingerprint.ts's hashFingerprint): exactly 64
        // characters, so an unbounded or malformed header can never reach the data tier.
        [GeneratedRegex("^[0-9a-f]{64}$")]
        private static partial Regex FingerprintShapeRegex();
    }
}
