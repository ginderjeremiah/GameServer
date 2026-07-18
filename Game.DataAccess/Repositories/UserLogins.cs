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

        // Bounds how many distinct devices a single account can accumulate. Legitimate players use a
        // handful of devices; without a cap, an authenticated caller cycling a fresh (validly-shaped)
        // fingerprint on every request would otherwise grow the Devices/UserLogins tables without bound
        // (#2064). Once reached, connections from a device the user hasn't already been seen on are
        // simply not tracked rather than erroring — tracking is best-effort telemetry, not a gate on play.
        private const int MaxTrackedDevicesPerUser = 20;

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
            deviceFingerprintHash = Truncate(deviceFingerprintHash, Device.MaxFingerprintLength) ?? string.Empty;

            return SaveWithConflictRetry(async () =>
            {
                var device = await _context.Devices.FirstOrDefaultAsync(
                    d => d.DeviceFingerprintHash == deviceFingerprintHash, cancellationToken);

                var login = device is null
                    ? null
                    : await _context.UserLogins.FirstOrDefaultAsync(l =>
                        l.UserId == userId && l.IpAddress == ipAddress && l.DeviceId == device.Id, cancellationToken);

                if (login is not null)
                {
                    login.LastConnection = DateTime.UtcNow;
                    return;
                }

                // A device (whether its row already exists globally or not) the user has no existing login
                // for is a *new* device from this user's perspective, so it grows their tracked-device count
                // and is subject to the cap.
                var isNewDeviceForUser = device is null ||
                    !await _context.UserLogins.AnyAsync(l => l.UserId == userId && l.DeviceId == device.Id, cancellationToken);

                if (isNewDeviceForUser)
                {
                    var trackedDeviceCount = await _context.UserLogins
                        .Where(l => l.UserId == userId)
                        .Select(l => l.DeviceId)
                        .Distinct()
                        .CountAsync(cancellationToken);

                    if (trackedDeviceCount >= MaxTrackedDevicesPerUser)
                    {
                        return;
                    }
                }

                device ??= await CreateDevice(deviceFingerprintHash, userAgent, secChUa, secChUaMobile, secChUaPlatform, cancellationToken);

                // Set via the navigation, not DeviceId directly — a just-created device has no generated Id
                // yet, and EF only fixes that up through the relationship once SaveChanges assigns one.
                _context.UserLogins.Add(new UserLogin
                {
                    UserId = userId,
                    IpAddress = ipAddress,
                    Device = device,
                    LastConnection = DateTime.UtcNow,
                });
            }, cancellationToken);
        }

        public Task SaveDeviceInfo(
            int userId,
            string deviceFingerprintHash,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform,
            double? deviceMemory,
            int? hardwareConcurrency,
            CancellationToken cancellationToken = default)
        {
            deviceFingerprintHash = Truncate(deviceFingerprintHash, Device.MaxFingerprintLength) ?? string.Empty;

            return SaveWithConflictRetry(async () =>
            {
                // Only enrich a device this user actually has a tracked login for — never create or attach
                // to one here — so a caller can't touch another account's device by guessing/replaying its
                // fingerprint (#2064). RecordConnection (which runs earlier in the same request, via
                // LoginTrackingMiddleware) is solely responsible for establishing that link.
                var device = await _context.Devices
                    .Include(d => d.BrowserInfo)
                    .FirstOrDefaultAsync(d => d.DeviceFingerprintHash == deviceFingerprintHash &&
                        _context.UserLogins.Any(l => l.UserId == userId && l.DeviceId == d.Id),
                        cancellationToken);

                if (device is null)
                {
                    return;
                }

                device.BrowserInfo.SecChUa ??= Truncate(secChUa, BrowserInfo.MaxClientHintLength);
                device.BrowserInfo.SecChUaMobile ??= Truncate(secChUaMobile, BrowserInfo.MaxClientHintLength);
                device.BrowserInfo.SecChUaPlatform ??= Truncate(secChUaPlatform, BrowserInfo.MaxClientHintLength);
                device.DeviceMemory = deviceMemory;
                device.HardwareConcurrency = hardwareConcurrency;
            }, cancellationToken);
        }

        /// <summary>Inserts a new <see cref="Device"/>, linked to the user-agent's <see cref="BrowserInfo"/>.</summary>
        private async Task<Device> CreateDevice(
            string deviceFingerprintHash,
            string userAgent,
            string? secChUa,
            string? secChUaMobile,
            string? secChUaPlatform,
            CancellationToken cancellationToken)
        {
            var browserInfo = await GetOrCreateBrowserInfo(userAgent, secChUa, secChUaMobile, secChUaPlatform, cancellationToken);
            var device = new Device
            {
                DeviceFingerprintHash = deviceFingerprintHash,
                BrowserInfo = browserInfo,
            };
            _context.Devices.Add(device);

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
