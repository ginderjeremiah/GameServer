using Game.Abstractions.DataAccess;
using Game.Infrastructure.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class UserLogins : IUserLogins
    {
        // The get-or-create rows are deduplicated by unique indexes (device fingerprint, browser
        // user-agent, the user/IP/device login key), so a concurrent connection from the same new
        // device turns a lost read-then-insert into a unique violation. One reload settles the device
        // and browser; a second covers the rare case where the login row itself raced too.
        private const int MaxSaveAttempts = 3;

        private readonly GameContext _context;

        public UserLogins(GameContext context)
        {
            _context = context;
        }

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
            ipAddress = Truncate(ipAddress, UserLogin.MaxIpAddressLength) ?? string.Empty;

            return SaveWithConflictRetry(async () =>
            {
                var device = await GetOrCreateDevice(deviceFingerprintHash, userAgent, secChUa, secChUaMobile, secChUaPlatform, cancellationToken);

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
                    l.UserId == userId && l.IpAddress == ipAddress && l.DeviceId == device.Id, cancellationToken);

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
            }, cancellationToken);
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
            return SaveWithConflictRetry(async () =>
            {
                var device = await GetOrCreateDevice(deviceFingerprintHash, userAgent, secChUa, secChUaMobile, secChUaPlatform, cancellationToken);

                device.DeviceMemory = deviceMemory;
                device.HardwareConcurrency = hardwareConcurrency;
            }, cancellationToken);
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
            string? secChUaPlatform,
            CancellationToken cancellationToken)
        {
            deviceFingerprintHash = Truncate(deviceFingerprintHash, Device.MaxFingerprintLength) ?? string.Empty;

            var device = await _context.Devices.FirstOrDefaultAsync(d => d.DeviceFingerprintHash == deviceFingerprintHash, cancellationToken);
            if (device is null)
            {
                var browserInfo = await GetOrCreateBrowserInfo(userAgent, secChUa, secChUaMobile, secChUaPlatform, cancellationToken);
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
            string? secChUaPlatform,
            CancellationToken cancellationToken)
        {
            userAgent = Truncate(userAgent, BrowserInfo.MaxUserAgentLength) ?? string.Empty;
            secChUa = Truncate(secChUa, BrowserInfo.MaxClientHintLength);
            secChUaMobile = Truncate(secChUaMobile, BrowserInfo.MaxClientHintLength);
            secChUaPlatform = Truncate(secChUaPlatform, BrowserInfo.MaxClientHintLength);

            var browserInfo = await _context.BrowserInfos.FirstOrDefaultAsync(b => b.UserAgent == userAgent, cancellationToken);
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

        /// <summary>
        /// Builds and persists the connection-tracking changes, owning its own commit (like
        /// <see cref="Users.CreateAccount"/>) because it runs in middleware outside the per-action commit
        /// filter and must retry the build on a unique violation. On a concurrent-insert conflict the
        /// rolled-back inserts are discarded so the rebuild re-queries the now-committed rows and updates
        /// in place instead of re-attempting the duplicate insert.
        /// </summary>
        private async Task SaveWithConflictRetry(Func<Task> buildChanges, CancellationToken cancellationToken)
        {
            for (var attempt = 1; ; attempt++)
            {
                await buildChanges();
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    return;
                }
                catch (DbUpdateException ex) when (attempt < MaxSaveAttempts && ex.IsUniqueViolation())
                {
                    _context.ChangeTracker.Clear();
                }
            }
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
