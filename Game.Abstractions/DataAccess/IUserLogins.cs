namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Persists lightweight user-connection tracking: a per-(user, IP, browser) <c>UserLogin</c> whose
    /// last-connection timestamp is continually refreshed, plus the deduplicated <c>BrowserInfo</c> the
    /// login references. Mutations are queued on the change tracker; the caller commits the unit of work.
    /// </summary>
    public interface IUserLogins
    {
        /// <summary>
        /// Records a connection for the given user from the given IP and browser. Resolves (creating when
        /// new) the <c>BrowserInfo</c> for the user-agent — capturing the client-hint headers on creation —
        /// then upserts the <c>UserLogin</c> for the (user, IP, browser) combination, setting its last
        /// connection to now.
        /// </summary>
        Task RecordConnection(
            int userId,
            string ipAddress,
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform);

        /// <summary>
        /// Enriches (creating when new) the <c>BrowserInfo</c> for the given user-agent with the device
        /// signals the frontend sends after login (fingerprint hash and capabilities), which are not present
        /// on a regular request. Provided client-hint headers are applied when their stored value is missing.
        /// </summary>
        Task SaveBrowserInfo(
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform,
            string? deviceFingerprintHash,
            double? deviceMemory,
            int? hardwareConcurrency);
    }
}
