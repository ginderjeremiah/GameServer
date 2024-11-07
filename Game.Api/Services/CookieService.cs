namespace Game.Api.Services
{
    public class CookieService
    {
        private readonly IHttpContextAccessor _context;

        private HttpContext Context => _context.HttpContext ?? throw new InvalidOperationException($"{nameof(CookieService)} cannot be used outside of an HTTP request.");

        public CookieService(IHttpContextAccessor context)
        {
            _context = context;
        }

        public string? GetTokenCookie()
        {
            return Context.Request.Cookies[Constants.TOKEN_NAME];
        }

        public void SetTokenCookie(string token)
        {
            Context.Response.Cookies.Append(Constants.TOKEN_NAME, token, new CookieOptions()
            {
                Secure = true,
                HttpOnly = true,
                SameSite = SameSiteMode.None,
                Expires = DateTime.UtcNow.Add(Constants.TOKEN_LIFETIME),
                IsEssential = true,
            });
        }
    }
}
