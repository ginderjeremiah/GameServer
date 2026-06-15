using Game.Api.Middleware;
using Microsoft.AspNetCore.Http;
using System.Net;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Covers <see cref="LoginTrackingMiddleware.ResolveIpAddress"/>: the <c>X-Forwarded-For</c>
    /// split/trim, the empty/whitespace fallback to the transport remote address, and the final
    /// "unknown" fallback — the multi-entry/whitespace branches that integration coverage missed.
    /// </summary>
    public class LoginTrackingIpResolutionTests
    {
        private static HttpContext ContextWith(string? forwardedFor, IPAddress? remoteIp)
        {
            var context = new DefaultHttpContext();
            if (forwardedFor is not null)
            {
                context.Request.Headers["X-Forwarded-For"] = forwardedFor;
            }
            context.Connection.RemoteIpAddress = remoteIp;
            return context;
        }

        [Fact]
        public void PrefersSingleForwardedForEntry_Trimmed()
        {
            var context = ContextWith("  203.0.113.5  ", IPAddress.Parse("10.0.0.1"));

            Assert.Equal("203.0.113.5", LoginTrackingMiddleware.ResolveIpAddress(context));
        }

        [Fact]
        public void TakesFirstEntry_FromMultiHopForwardedFor()
        {
            var context = ContextWith("203.0.113.5, 70.41.3.18, 150.172.238.178", IPAddress.Parse("10.0.0.1"));

            Assert.Equal("203.0.113.5", LoginTrackingMiddleware.ResolveIpAddress(context));
        }

        [Fact]
        public void FallsBackToRemoteAddress_WhenForwardedForMissing()
        {
            var context = ContextWith(forwardedFor: null, IPAddress.Parse("198.51.100.7"));

            Assert.Equal("198.51.100.7", LoginTrackingMiddleware.ResolveIpAddress(context));
        }

        [Fact]
        public void FallsBackToRemoteAddress_WhenForwardedForIsWhitespace()
        {
            var context = ContextWith("   ", IPAddress.Parse("198.51.100.7"));

            Assert.Equal("198.51.100.7", LoginTrackingMiddleware.ResolveIpAddress(context));
        }

        [Fact]
        public void ReturnsUnknown_WhenNoForwardedForAndNoRemoteAddress()
        {
            var context = ContextWith(forwardedFor: null, remoteIp: null);

            Assert.Equal("unknown", LoginTrackingMiddleware.ResolveIpAddress(context));
        }
    }
}
