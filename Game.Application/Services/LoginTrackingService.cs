using Game.Abstractions.DataAccess;

namespace Game.Application.Services
{
    /// <summary>
    /// Orchestrates user-connection tracking: recording connections (last-connection upserts) and
    /// enriching the stored device with the capabilities the frontend reports after login. Each operation
    /// persists itself via the unit of work, since the connection-recording caller runs in middleware,
    /// outside the per-action commit pipeline.
    /// </summary>
    public class LoginTrackingService(IUserLogins userLogins, IUnitOfWork unitOfWork)
    {
        private readonly IUserLogins _userLogins = userLogins;
        private readonly IUnitOfWork _unitOfWork = unitOfWork;

        public async Task RecordConnection(
            int userId,
            string ipAddress,
            string deviceFingerprintHash,
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform,
            CancellationToken cancellationToken = default)
        {
            await _userLogins.RecordConnection(
                userId, ipAddress, deviceFingerprintHash, userAgent, secChUa, secChUaMobile, secChUaPlatform, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }

        public async Task SaveDeviceInfo(
            string deviceFingerprintHash,
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform,
            double? deviceMemory,
            int? hardwareConcurrency,
            CancellationToken cancellationToken = default)
        {
            await _userLogins.SaveDeviceInfo(
                deviceFingerprintHash, userAgent, secChUa, secChUaMobile, secChUaPlatform,
                deviceMemory, hardwareConcurrency, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
    }
}
