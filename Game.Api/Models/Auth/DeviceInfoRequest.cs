namespace Game.Api.Models.Auth
{
    /// <summary>
    /// Device capabilities the frontend reports once after login. The device fingerprint that identifies
    /// the device is sent as a request header (on every authenticated request), and the user-agent and
    /// client-hint headers are read server-side, so only the JS-only capabilities are carried in the body.
    /// </summary>
    public class DeviceInfoRequest : IModel
    {
        /// <summary>Approximate device memory in GiB (<c>navigator.deviceMemory</c>).</summary>
        public double? DeviceMemory { get; set; }

        /// <summary>Logical processor count (<c>navigator.hardwareConcurrency</c>).</summary>
        public int? HardwareConcurrency { get; set; }
    }
}
