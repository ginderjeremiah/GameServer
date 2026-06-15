using Game.Abstractions.Auth;
using Game.Api.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Security-critical coverage for the JWT access-token issuance. Asserts that a token issued by
    /// <see cref="JwtTokenService"/> validates against the same <see cref="TokenValidationParameters"/>
    /// the bearer handler is configured with in <c>Startup.ConfigureAuth</c>, and that the claims,
    /// signing algorithm and lifetime match the documented contract.
    /// </summary>
    public class JwtTokenServiceTests
    {
        // 32+ bytes so the HMAC-SHA256 signing key is large enough.
        private const string SigningKey = "this-is-a-sufficiently-long-test-signing-key-0123456789";
        private const string Issuer = "test-issuer";
        private const string Audience = "test-audience";

        private static JwtTokenService CreateService() => new(Options.Create(new JwtOptions
        {
            SigningKey = SigningKey,
            Issuer = Issuer,
            Audience = Audience,
        }));

        // Mirrors the parameters the bearer handler is configured with in Startup.ConfigureAuth so the
        // test pins that issuance and validation agree.
        private static TokenValidationParameters ValidationParameters() => new()
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = JwtTokenService.RoleClaimType,
        };

        [Fact]
        public async Task CreateAccessToken_IssuesTokenThatValidatesAgainstStartupParameters()
        {
            var token = CreateService().CreateAccessToken(42, ["Admin"]);

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, ValidationParameters());

            Assert.True(result.IsValid, result.Exception?.Message);
        }

        [Fact]
        public async Task CreateAccessToken_SubClaimIsUserId_AndIsHmacSha256Signed()
        {
            var token = CreateService().CreateAccessToken(123, []);

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, ValidationParameters());
            var jwt = Assert.IsType<JsonWebToken>(result.SecurityToken);

            Assert.Equal("123", jwt.GetClaim(JwtRegisteredClaimNames.Sub).Value);
            Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Alg);
            Assert.Equal(Issuer, jwt.Issuer);
            // A unique token id is stamped per issuance.
            Assert.False(string.IsNullOrEmpty(jwt.GetClaim(JwtRegisteredClaimNames.Jti).Value));
        }

        [Fact]
        public async Task CreateAccessToken_MapsEveryRoleOntoTheRoleClaimType()
        {
            var token = CreateService().CreateAccessToken(7, ["Admin", "Moderator"]);

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, ValidationParameters());
            var principal = new ClaimsPrincipal(result.ClaimsIdentity);

            Assert.True(principal.IsInRole("Admin"));
            Assert.True(principal.IsInRole("Moderator"));
            var roleValues = principal.FindAll(JwtTokenService.RoleClaimType).Select(c => c.Value).ToList();
            Assert.Equal(["Admin", "Moderator"], roleValues);
        }

        [Fact]
        public void CreateAccessToken_NoRoles_EmitsNoRoleClaims()
        {
            var token = CreateService().CreateAccessToken(7, []);

            var jwt = new JsonWebToken(token);

            Assert.DoesNotContain(jwt.Claims, c => c.Type == JwtTokenService.RoleClaimType);
        }

        [Fact]
        public void CreateAccessToken_ExpiryMatchesConfiguredAccessTokenLifetime()
        {
            var token = CreateService().CreateAccessToken(1, []);

            var jwt = new JsonWebToken(token);

            // The token lives exactly AuthConstants.AccessTokenLifetime from its not-before instant.
            Assert.Equal(AuthConstants.AccessTokenLifetime, jwt.ValidTo - jwt.ValidFrom);
        }
    }
}
