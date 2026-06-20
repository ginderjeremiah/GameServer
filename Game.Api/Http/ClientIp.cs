using Microsoft.AspNetCore.Http;

namespace Game.Api.Http
{
    /// <summary>
    /// Resolves the originating client IP for a request, used as the partition key for rate limiting and
    /// the recorded address for login tracking. It reads the transport remote address, which the
    /// forwarded-headers middleware corrects to the real client only when the request arrives through a
    /// configured trusted proxy — so a spoofed <c>X-Forwarded-For</c> from a direct client is never
    /// honoured (#910). Falls back to "unknown" when there is no remote address.
    /// </summary>
    internal static class ClientIp
    {
        internal static string Resolve(HttpContext context)
        {
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
