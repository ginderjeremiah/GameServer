using Microsoft.AspNetCore.Authentication;

namespace GameServer.Auth
{
    public class SessionAuthHandler : IAuthenticationHandler
    {
        private HttpContext _context;

        public Task InitializeAsync(AuthenticationScheme scheme, HttpContext context)
        {
            _context = context;
            return Task.CompletedTask;
        }

        public Task<AuthenticateResult> AuthenticateAsync()
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(AuthenticationProperties? properties)
        {
            _context.Response.StatusCode = 401;
            return Task.FromResult(AuthenticateResult.Fail("Not Authenticated!"));
        }

        public Task ForbidAsync(AuthenticationProperties? properties)
        {
            _context.Response.StatusCode = 403;
            return Task.FromResult(AuthenticateResult.Fail("Not Authenticated!"));
        }
    }
}
