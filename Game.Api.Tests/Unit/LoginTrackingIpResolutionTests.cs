using Game.Api.Middleware;
using Microsoft.AspNetCore.Http;
using System.Net;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Covers <see cref="LoginTrackingMiddleware.ResolveIpAddress"/>: it reads the transport remote address
    /// (corrected by the forwarded-headers middleware only for trusted proxies), never trusts a raw
    /// <c>X-Forwarded-For</c> header itself, and falls back to "unknown" when there is no remote address.
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
        public void UsesRemoteAddress()
        {
            var context = ContextWith(forwardedFor: null, IPAddress.Parse("198.51.100.7"));

            Assert.Equal("198.51.100.7", LoginTrackingMiddleware.ResolveIpAddress(context));
        }

        [Fact]
        public void IgnoresSpoofedForwardedForHeader()
        {
            // A direct client setting X-Forwarded-For must not influence the resolved IP — only the
            // forwarded-headers middleware (for a trusted proxy) may rewrite RemoteIpAddress.
            var context = ContextWith("203.0.113.5", IPAddress.Parse("198.51.100.7"));

            Assert.Equal("198.51.100.7", LoginTrackingMiddleware.ResolveIpAddress(context));
        }

        [Fact]
        public void ReturnsUnknown_WhenNoRemoteAddress()
        {
            var context = ContextWith(forwardedFor: null, remoteIp: null);

            Assert.Equal("unknown", LoginTrackingMiddleware.ResolveIpAddress(context));
        }
    }
}
