using Game.Abstractions.DataAccess;

namespace Game.Application.Services
{
    /// <summary>
    /// Orchestrates user-connection tracking: recording connections (last-connection upserts) and
    /// enriching the stored device with the capabilities the frontend reports after login. The data tier
    /// owns the commit for these operations (like account creation) because each runs outside the
    /// per-action commit filter and must retry its build-and-save on a concurrent-insert unique violation.
    /// <para>
    /// This is a deliberately thin application-layer seam over <see cref="IUserLogins"/> — it keeps the API
    /// layer depending on an application service rather than reaching straight into a data-access interface,
    /// matching the rest of the orchestration layer, and is the natural home for any future tracking logic.
    /// It is intentionally not collapsed into the controller.
    /// </para>
    /// </summary>
    public class LoginTrackingService(IUserLogins userLogins)
    {
        private readonly IUserLogins _userLogins = userLogins;

        public Task RecordConnection(
            int userId,
            string ipAddress,
            string deviceFingerprintHash,
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform,
            CancellationToken cancellationToken = default)
        {
            return _userLogins.RecordConnection(
                userId, ipAddress, deviceFingerprintHash, userAgent, secChUa, secChUaMobile, secChUaPlatform, cancellationToken);
        }

        public Task SaveDeviceInfo(
            string deviceFingerprintHash,
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform,
            double? deviceMemory,
            int? hardwareConcurrency,
            CancellationToken cancellationToken = default)
        {
            return _userLogins.SaveDeviceInfo(
                deviceFingerprintHash, userAgent, secChUa, secChUaMobile, secChUaPlatform,
                deviceMemory, hardwareConcurrency, cancellationToken);
        }
    }
}
