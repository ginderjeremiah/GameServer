namespace Game.Abstractions.Entities
{
    /// <summary>
    /// A browser profile, deduplicated by its <see cref="UserAgent"/> string. Holds only the
    /// server-observable signals carried on every request (the user-agent and low-entropy client-hint
    /// headers), so it is genuinely shared across the many <see cref="Device"/>s that report the same
    /// user-agent. Device-specific data (fingerprint, capabilities) lives on <see cref="Device"/>.
    /// </summary>
    public class BrowserInfo
    {
        public const int MaxUserAgentLength = 512;
        public const int MaxClientHintLength = 256;

        public int Id { get; set; }

        /// <summary>The full <c>User-Agent</c> header string; the natural dedup key for a browser profile.</summary>
        public required string UserAgent { get; set; }

        /// <summary>The <c>Sec-CH-UA</c> client-hint header (browser brand/version list), when sent.</summary>
        public string? SecChUa { get; set; }

        /// <summary>The <c>Sec-CH-UA-Mobile</c> client-hint header, when sent.</summary>
        public string? SecChUaMobile { get; set; }

        /// <summary>The <c>Sec-CH-UA-Platform</c> client-hint header, when sent.</summary>
        public string? SecChUaPlatform { get; set; }

        public virtual List<Device> Devices { get => field ?? throw new NotLoadedException(nameof(Devices)); set; }
    }
}
