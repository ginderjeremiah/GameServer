using Game.Api.Forwarding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Covers <see cref="ForwardedHeadersConfig.Apply"/>: the framework defaults (loopback) are cleared so
    /// the trust set is entirely explicit, only X-Forwarded-For is honoured, the trust-chain depth
    /// (ForwardLimit) is projected (defaulting to 1), and configured proxies/networks are parsed onto the
    /// options while unparseable entries are skipped.
    /// </summary>
    public class ForwardedHeadersConfigTests
    {
        [Fact]
        public void HasTrustedProxies_IsFalse_WhenNothingConfigured()
        {
            Assert.False(new ForwardedHeadersConfig().HasTrustedProxies);
        }

        [Fact]
        public void HasTrustedProxies_IsTrue_WhenAProxyOrNetworkIsConfigured()
        {
            Assert.True(new ForwardedHeadersConfig { KnownProxies = ["10.0.0.1"] }.HasTrustedProxies);
            Assert.True(new ForwardedHeadersConfig { KnownNetworks = ["10.0.0.0/8"] }.HasTrustedProxies);
        }

        [Fact]
        public void Apply_WithEmptyConfig_TrustsNothingAndClearsLoopbackDefaults()
        {
            var options = new ForwardedHeadersOptions();
            // ASP.NET Core seeds these with loopback by default.
            options.KnownProxies.Add(IPAddress.Loopback);

            new ForwardedHeadersConfig().Apply(options);

            Assert.Equal(ForwardedHeaders.XForwardedFor, options.ForwardedHeaders);
            Assert.Empty(options.KnownProxies);
            Assert.Empty(options.KnownIPNetworks);
        }

        [Fact]
        public void Apply_ParsesConfiguredProxiesAndNetworks()
        {
            var options = new ForwardedHeadersOptions();
            var config = new ForwardedHeadersConfig
            {
                KnownProxies = ["10.0.0.1", "::1"],
                KnownNetworks = ["172.16.0.0/12"],
            };

            config.Apply(options);

            Assert.Equal(2, options.KnownProxies.Count);
            Assert.Contains(options.KnownProxies, p => p.Equals(IPAddress.Parse("10.0.0.1")));
            Assert.Contains(options.KnownProxies, p => p.Equals(IPAddress.Parse("::1")));
            Assert.Single(options.KnownIPNetworks);
            Assert.Equal(System.Net.IPNetwork.Parse("172.16.0.0/12"), options.KnownIPNetworks[0]);
        }

        [Fact]
        public void Apply_DefaultsForwardLimitToOne()
        {
            var options = new ForwardedHeadersOptions();

            new ForwardedHeadersConfig().Apply(options);

            Assert.Equal(1, options.ForwardLimit);
        }

        [Fact]
        public void Apply_SetsConfiguredForwardLimit()
        {
            var options = new ForwardedHeadersOptions();

            new ForwardedHeadersConfig { ForwardLimit = 2 }.Apply(options);

            Assert.Equal(2, options.ForwardLimit);
        }

        [Fact]
        public void Apply_WithNullForwardLimit_DisablesTheLimit()
        {
            var options = new ForwardedHeadersOptions();

            new ForwardedHeadersConfig { ForwardLimit = null }.Apply(options);

            Assert.Null(options.ForwardLimit);
        }

        [Fact]
        public void Apply_SkipsUnparseableEntries()
        {
            var options = new ForwardedHeadersOptions();
            var config = new ForwardedHeadersConfig
            {
                KnownProxies = ["not-an-ip", "10.0.0.2"],
                KnownNetworks = ["nonsense", "10.0.0.0/8"],
            };

            config.Apply(options);

            Assert.Single(options.KnownProxies);
            Assert.Equal(IPAddress.Parse("10.0.0.2"), options.KnownProxies[0]);
            Assert.Single(options.KnownIPNetworks);
        }
    }
}
