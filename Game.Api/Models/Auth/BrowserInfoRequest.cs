namespace Game.Api.Models.Auth
{
    /// <summary>
    /// Browser/device signals the frontend reports once after login. These are not present on a regular
    /// request (the user-agent and client-hint headers are read server-side from the request itself), so
    /// they are sent explicitly to enrich the stored browser profile.
    /// </summary>
    public class BrowserInfoRequest : IModel
    {
        /// <summary>A hash of stable client-side signals identifying the device.</summary>
        public string? DeviceFingerprintHash { get; set; }

        /// <summary>Approximate device memory in GiB (<c>navigator.deviceMemory</c>).</summary>
        public double? DeviceMemory { get; set; }

        /// <summary>Logical processor count (<c>navigator.hardwareConcurrency</c>).</summary>
        public int? HardwareConcurrency { get; set; }
    }
}
