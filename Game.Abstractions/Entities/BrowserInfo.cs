namespace Game.Abstractions.Entities
{
    /// <summary>
    /// A distinct browser/device profile, deduplicated by its <see cref="UserAgent"/> string. The
    /// low-entropy client-hint headers are captured server-side from request headers, while the
    /// fingerprint hash and device capabilities are sent separately by the frontend after login (they
    /// are not present on a regular request). Shared across users via <see cref="UserLogin"/>.
    /// </summary>
    public class BrowserInfo
    {
        public const int MaxUserAgentLength = 512;
        public const int MaxClientHintLength = 256;
        public const int MaxFingerprintLength = 128;

        public int Id { get; set; }

        /// <summary>The full <c>User-Agent</c> header string; the natural dedup key for a browser profile.</summary>
        public required string UserAgent { get; set; }

        /// <summary>The <c>Sec-CH-UA</c> client-hint header (browser brand/version list), when sent.</summary>
        public string? SecChUa { get; set; }

        /// <summary>The <c>Sec-CH-UA-Mobile</c> client-hint header, when sent.</summary>
        public string? SecChUaMobile { get; set; }

        /// <summary>The <c>Sec-CH-UA-Platform</c> client-hint header, when sent.</summary>
        public string? SecChUaPlatform { get; set; }

        /// <summary>A hash of stable client-side signals, computed and sent by the frontend after login.</summary>
        public string? DeviceFingerprintHash { get; set; }

        /// <summary>Approximate device memory in GiB (<c>navigator.deviceMemory</c>), sent by the frontend.</summary>
        public double? DeviceMemory { get; set; }

        /// <summary>Logical processor count (<c>navigator.hardwareConcurrency</c>), sent by the frontend.</summary>
        public int? HardwareConcurrency { get; set; }

        public virtual List<UserLogin> UserLogins { get => field ?? throw new NotLoadedException(nameof(UserLogins)); set; }
    }
}
