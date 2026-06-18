using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace Game.Api.Forwarding
{
    /// <summary>
    /// Deployment-specific allowlist of reverse proxies whose <c>X-Forwarded-For</c> header is trusted,
    /// bound from the "ForwardedHeaders" configuration section. Both lists default to empty, so unless a
    /// deployment explicitly configures its proxies, no <c>X-Forwarded-For</c> is honoured and a request's
    /// IP stays the socket peer address — a spoofed header from a direct client is ignored.
    /// </summary>
    public class ForwardedHeadersConfig
    {
        /// <summary>The configuration section this options class binds from.</summary>
        public const string SectionName = "ForwardedHeaders";

        /// <summary>Individual proxy IP addresses (e.g. <c>"127.0.0.1"</c>, <c>"::1"</c>) to trust.</summary>
        public List<string> KnownProxies { get; set; } = [];

        /// <summary>Proxy networks in CIDR form (e.g. <c>"10.0.0.0/8"</c>) to trust.</summary>
        public List<string> KnownNetworks { get; set; } = [];

        /// <summary>
        /// Whether any trusted proxy is configured. When false the forwarded-headers middleware must not
        /// run at all: with an empty allowlist it would skip its known-proxy check and trust the header
        /// unconditionally, which is exactly the spoofing we are guarding against.
        /// </summary>
        public bool HasTrustedProxies => KnownProxies.Count > 0 || KnownNetworks.Count > 0;

        /// <summary>
        /// Projects this config onto the framework's <see cref="ForwardedHeadersOptions"/>, honouring only
        /// <c>X-Forwarded-For</c> from the configured proxies/networks. ASP.NET Core defaults the known
        /// proxies/networks to loopback, so the framework lists are cleared first and repopulated solely
        /// from configuration — making the trust set entirely explicit and "trust nothing" the safe default.
        /// </summary>
        public void Apply(ForwardedHeadersOptions options)
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var proxy in KnownProxies)
            {
                if (IPAddress.TryParse(proxy, out var address))
                {
                    options.KnownProxies.Add(address);
                }
            }

            foreach (var network in KnownNetworks)
            {
                if (System.Net.IPNetwork.TryParse(network, out var parsed))
                {
                    options.KnownIPNetworks.Add(parsed);
                }
            }
        }
    }
}
