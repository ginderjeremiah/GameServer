namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Persists lightweight user-connection tracking: a per-(user, IP, device) <c>UserLogin</c> whose
    /// last-connection timestamp is continually refreshed, the <c>Device</c> (deduplicated by fingerprint)
    /// the login references, and the <c>BrowserInfo</c> (deduplicated by user-agent) the device reports.
    /// Each operation <b>owns its own commit</b> (like account creation) because it runs outside the
    /// per-action commit filter and must retry its build-and-save on a concurrent-insert unique violation.
    /// </summary>
    public interface IUserLogins
    {
        /// <summary>
        /// Records a connection for the given user from the given IP and device. Resolves (creating when
        /// new) the <c>Device</c> for the fingerprint — and its <c>BrowserInfo</c> for the user-agent,
        /// capturing the client-hint headers on creation — then upserts the <c>UserLogin</c> for the
        /// (user, IP, device) combination, setting its last connection to now.
        /// </summary>
        Task RecordConnection(
            int userId,
            string ipAddress,
            string deviceFingerprintHash,
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Enriches the caller's own <c>Device</c> for the given fingerprint with the capabilities the
        /// frontend reports after login (<c>deviceMemory</c>/<c>hardwareConcurrency</c>), backfilling any
        /// client-hint the stored <c>BrowserInfo</c> is still missing. A no-op when <paramref name="userId"/>
        /// has no tracked login for that fingerprint — this path never creates or attaches a <c>Device</c>,
        /// so a caller can't enrich (or overwrite) a device it hasn't actually connected from, even if it
        /// knows the fingerprint.
        /// </summary>
        Task SaveDeviceInfo(
            int userId,
            string deviceFingerprintHash,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform,
            double? deviceMemory,
            int? hardwareConcurrency,
            CancellationToken cancellationToken = default);
    }
}
