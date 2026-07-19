namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// A distinct device, deduplicated by its client-computed <see cref="DeviceFingerprintHash"/> (the most
    /// device-unique signal available). Each device is linked to the <see cref="Entities.BrowserInfo"/>
    /// (user-agent) profile seen when the device was first created; the link is set once and never
    /// revisited afterward. The fingerprint itself deliberately excludes the raw user-agent string (using a
    /// coarse, update-stable platform token instead, so a browser auto-update doesn't rotate the
    /// fingerprint), so a browser upgrade — or a switch to a different browser reporting the same coarse
    /// platform/hardware/locale signals — can leave a device pointing at a stale <see cref="BrowserInfo"/>.
    /// This is fine because the link is presentation/telemetry data only, never used for authorization or
    /// gameplay decisions. The capabilities are reported by the frontend after login, since they are not
    /// present on a regular request.
    /// </summary>
    public class Device
    {
        public const int MaxFingerprintLength = 128;

        public int Id { get; set; }

        /// <summary>A hash of stable client-side signals identifying the device; the dedup key.</summary>
        public required string DeviceFingerprintHash { get; set; }

        /// <summary>Foreign key to the <see cref="Entities.BrowserInfo"/> (user-agent) profile this device reports.</summary>
        public int BrowserInfoId { get; set; }

        /// <summary>Approximate device memory in GiB (<c>navigator.deviceMemory</c>), reported by the frontend.</summary>
        public double? DeviceMemory { get; set; }

        /// <summary>Logical processor count (<c>navigator.hardwareConcurrency</c>), reported by the frontend.</summary>
        public int? HardwareConcurrency { get; set; }

        public virtual BrowserInfo BrowserInfo { get => field ?? throw new NotLoadedException(nameof(BrowserInfo)); set; }

        public virtual List<UserLogin> UserLogins { get => field ?? throw new NotLoadedException(nameof(UserLogins)); set; }
    }
}
