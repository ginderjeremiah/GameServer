using Game.Abstractions.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace Game.Api.Auth
{
    /// <summary>
    /// Issues short-lived signed JWT access tokens using the standard .NET token libraries
    /// (<see cref="JsonWebTokenHandler"/>). Tokens are symmetrically signed (HMAC-SHA256) and carry the
    /// user id (<c>sub</c>) and role claims, which the JWT bearer authentication handler validates and
    /// projects onto <see cref="ClaimsPrincipal"/> for the request.
    /// </summary>
    public class JwtTokenService : IAccessTokenService
    {
        /// <summary>
        /// The claim type used for role claims. Matches the role claim type configured on the bearer
        /// token validation parameters so <c>User.IsInRole</c> and role-based authorization work.
        /// </summary>
        public const string RoleClaimType = "role";

        private readonly JwtOptions _options;
        private readonly SymmetricSecurityKey _signingKey;
        // JsonWebTokenHandler is thread-safe and caches signing material internally, so a single
        // instance is reused across all token issuance rather than allocated per call.
        private readonly JsonWebTokenHandler _handler = new();

        public JwtTokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value;
            _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        }

        public string CreateAccessToken(int userId, IReadOnlyList<string> roles)
        {
            var now = DateTime.UtcNow;
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };
            claims.AddRange(roles.Select(role => new Claim(RoleClaimType, role)));

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                Subject = new ClaimsIdentity(claims),
                NotBefore = now,
                Expires = now.Add(AuthConstants.AccessTokenLifetime),
                SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256),
            };

            return _handler.CreateToken(descriptor);
        }
    }
}
