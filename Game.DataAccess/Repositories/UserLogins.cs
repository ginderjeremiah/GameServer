using Game.Abstractions.DataAccess;
using Game.Infrastructure.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class UserLogins : IUserLogins
    {
        private readonly GameContext _context;

        public UserLogins(GameContext context)
        {
            _context = context;
        }

        public async Task RecordConnection(
            int userId,
            string ipAddress,
            string deviceFingerprintHash,
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform)
        {
            ipAddress = Truncate(ipAddress, UserLogin.MaxIpAddressLength) ?? string.Empty;

            var device = await GetOrCreateDevice(deviceFingerprintHash, userAgent, secChUa, secChUaMobile, secChUaPlatform);

            // A brand-new device cannot have an existing login, so skip the lookup and rely on the
            // navigation to propagate the generated DeviceId onto the new login when saved.
            if (_context.Entry(device).State == EntityState.Added)
            {
                _context.UserLogins.Add(new UserLogin
                {
                    UserId = userId,
                    IpAddress = ipAddress,
                    Device = device,
                    LastConnection = DateTime.UtcNow,
                });
                return;
            }

            var login = await _context.UserLogins.FirstOrDefaultAsync(l =>
                l.UserId == userId && l.IpAddress == ipAddress && l.DeviceId == device.Id);

            if (login is null)
            {
                _context.UserLogins.Add(new UserLogin
                {
                    UserId = userId,
                    IpAddress = ipAddress,
                    DeviceId = device.Id,
                    LastConnection = DateTime.UtcNow,
                });
            }
            else
            {
                login.LastConnection = DateTime.UtcNow;
            }
        }

        public async Task SaveDeviceInfo(
            string deviceFingerprintHash,
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform,
            double? deviceMemory,
            int? hardwareConcurrency)
        {
            var device = await GetOrCreateDevice(deviceFingerprintHash, userAgent, secChUa, secChUaMobile, secChUaPlatform);

            device.DeviceMemory = deviceMemory;
            device.HardwareConcurrency = hardwareConcurrency;
        }

        /// <summary>
        /// Returns the tracked <see cref="Device"/> for the fingerprint, adding a new one (linked to the
        /// user-agent's <see cref="BrowserInfo"/>) when none exists yet.
        /// </summary>
        private async Task<Device> GetOrCreateDevice(
            string deviceFingerprintHash,
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform)
        {
            deviceFingerprintHash = Truncate(deviceFingerprintHash, Device.MaxFingerprintLength) ?? string.Empty;

            var device = await _context.Devices.FirstOrDefaultAsync(d => d.DeviceFingerprintHash == deviceFingerprintHash);
            if (device is null)
            {
                var browserInfo = await GetOrCreateBrowserInfo(userAgent, secChUa, secChUaMobile, secChUaPlatform);
                device = new Device
                {
                    DeviceFingerprintHash = deviceFingerprintHash,
                    BrowserInfo = browserInfo,
                };
                _context.Devices.Add(device);
            }

            return device;
        }

        /// <summary>
        /// Returns the tracked <see cref="BrowserInfo"/> for the user-agent, adding a new one (with the
        /// client-hint headers) when none exists yet. Client hints are only filled in when missing so a
        /// later request with fewer hints never clears values an earlier one captured.
        /// </summary>
        private async Task<BrowserInfo> GetOrCreateBrowserInfo(
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform)
        {
            userAgent = Truncate(userAgent, BrowserInfo.MaxUserAgentLength) ?? string.Empty;
            secChUa = Truncate(secChUa, BrowserInfo.MaxClientHintLength);
            secChUaMobile = Truncate(secChUaMobile, BrowserInfo.MaxClientHintLength);
            secChUaPlatform = Truncate(secChUaPlatform, BrowserInfo.MaxClientHintLength);

            var browserInfo = await _context.BrowserInfos.FirstOrDefaultAsync(b => b.UserAgent == userAgent);
            if (browserInfo is null)
            {
                browserInfo = new BrowserInfo
                {
                    UserAgent = userAgent,
                    SecChUa = secChUa,
                    SecChUaMobile = secChUaMobile,
                    SecChUaPlatform = secChUaPlatform,
                };
                _context.BrowserInfos.Add(browserInfo);
            }
            else
            {
                browserInfo.SecChUa ??= secChUa;
                browserInfo.SecChUaMobile ??= secChUaMobile;
                browserInfo.SecChUaPlatform ??= secChUaPlatform;
            }

            return browserInfo;
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength];
        }
    }
}
