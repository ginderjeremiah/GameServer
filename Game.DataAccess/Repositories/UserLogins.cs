using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
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
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform)
        {
            ipAddress = Truncate(ipAddress, UserLogin.MaxIpAddressLength) ?? string.Empty;

            var browserInfo = await GetOrCreateBrowserInfo(userAgent, secChUa, secChUaMobile, secChUaPlatform);

            // A brand-new browser cannot have an existing login, so skip the lookup and rely on the
            // navigation to propagate the generated BrowserInfoId onto the new login when saved.
            if (_context.Entry(browserInfo).State == EntityState.Added)
            {
                _context.UserLogins.Add(new UserLogin
                {
                    UserId = userId,
                    IpAddress = ipAddress,
                    BrowserInfo = browserInfo,
                    LastConnection = DateTime.UtcNow,
                });
                return;
            }

            var login = await _context.UserLogins.FirstOrDefaultAsync(l =>
                l.UserId == userId && l.IpAddress == ipAddress && l.BrowserInfoId == browserInfo.Id);

            if (login is null)
            {
                _context.UserLogins.Add(new UserLogin
                {
                    UserId = userId,
                    IpAddress = ipAddress,
                    BrowserInfoId = browserInfo.Id,
                    LastConnection = DateTime.UtcNow,
                });
            }
            else
            {
                login.LastConnection = DateTime.UtcNow;
            }
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
            var browserInfo = await GetOrCreateBrowserInfo(userAgent, secChUa, secChUaMobile, secChUaPlatform);

            browserInfo.DeviceFingerprintHash = Truncate(deviceFingerprintHash, BrowserInfo.MaxFingerprintLength);
            browserInfo.DeviceMemory = deviceMemory;
            browserInfo.HardwareConcurrency = hardwareConcurrency;
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
