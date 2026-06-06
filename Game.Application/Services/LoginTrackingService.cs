using Game.Abstractions.DataAccess;

namespace Game.Application.Services
{
    /// <summary>
    /// Orchestrates user-connection tracking: recording connections (last-connection upserts) and
    /// enriching the stored browser profile with the device signals the frontend reports after login.
    /// Each operation persists itself via the unit of work, since the connection-recording caller runs in
    /// middleware, outside the per-action commit pipeline.
    /// </summary>
    public class LoginTrackingService(IUserLogins userLogins, IUnitOfWork unitOfWork)
    {
        private readonly IUserLogins _userLogins = userLogins;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;

        public async Task RecordConnection(
            int userId,
            string ipAddress,
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform)
        {
            await _userLogins.RecordConnection(userId, ipAddress, userAgent, secChUa, secChUaMobile, secChUaPlatform);
            await _unitOfWork.CommitAsync();
        }

        public async Task SaveBrowserInfo(
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform,
            string? deviceFingerprintHash,
            double? deviceMemory,
            int? hardwareConcurrency)
        {
            await _userLogins.SaveBrowserInfo(
                userAgent, secChUa, secChUaMobile, secChUaPlatform,
                deviceFingerprintHash, deviceMemory, hardwareConcurrency);
            await _unitOfWork.CommitAsync();
        }
    }
}
