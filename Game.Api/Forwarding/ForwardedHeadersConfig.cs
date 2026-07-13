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
        /// How many <c>X-Forwarded-For</c> entries to walk back from the socket peer inward — the depth of
        /// the trusted proxy chain. Defaults to <c>1</c> (a single proxy), matching the framework default,
        /// so single-proxy deployments are unchanged. A legitimate multi-hop chain (e.g. CDN → ingress) must
        /// raise this to the chain length so the real client IP — not the last proxy's — reaches
        /// <c>RemoteIpAddress</c> (and therefore the rate-limiter/login-backoff partition key). <c>null</c>
        /// disables the limit (walk every entry), which is only safe when every hop is on the trust allowlist.
        /// </summary>
        public int? ForwardLimit { get; set; } = 1;

        /// <summary>
        /// Whether any trusted proxy is configured. When false the forwarded-headers middleware must not
        /// run at all: with an empty allowlist it would skip its known-proxy check and trust the header
        /// unconditionally, which is exactly the spoofing we are guarding against.
        /// </summary>
        public bool HasTrustedProxies => KnownProxies.Count > 0 || KnownNetworks.Count > 0;

        /// <summary>
        /// Whether every configured <see cref="KnownProxies"/>/<see cref="KnownNetworks"/> entry parses.
        /// Validated at startup (<c>ValidateOnStart</c>) so a config typo — e.g. a hostname in
        /// <c>KnownProxies</c>, or a CIDR put in the wrong list — fails fast instead of being silently
        /// dropped by <see cref="Apply"/>. Without this check, an all-unparseable config would leave
        /// <see cref="HasTrustedProxies"/> true (the raw strings are still present) while the framework's
        /// trust lists end up empty — the exact state that makes ASP.NET Core's forwarded-headers
        /// middleware trust <c>X-Forwarded-For</c> unconditionally.
        /// </summary>
        public bool AllEntriesParse =>
            KnownProxies.All(proxy => IPAddress.TryParse(proxy, out _)) &&
            KnownNetworks.All(network => System.Net.IPNetwork.TryParse(network, out _));

        /// <summary>
        /// Projects this config onto the framework's <see cref="ForwardedHeadersOptions"/>, honouring only
        /// <c>X-Forwarded-For</c> from the configured proxies/networks. ASP.NET Core defaults the known
        /// proxies/networks to loopback, so the framework lists are cleared first and repopulated solely
        /// from configuration — making the trust set entirely explicit and "trust nothing" the safe default.
        /// The trust depth (<see cref="ForwardLimit"/>) is the other half of that explicit trust set.
        /// </summary>
        public void Apply(ForwardedHeadersOptions options)
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            options.ForwardLimit = ForwardLimit;
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
