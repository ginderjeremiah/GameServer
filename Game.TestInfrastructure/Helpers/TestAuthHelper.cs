using Game.Api.Auth;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace Game.TestInfrastructure.Helpers
{
    public static class TestAuthHelper
    {
        public const string TestPepper = "test-pepper-value-for-integration-tests";

        /// <summary>
        /// Must match the Jwt:SigningKey configured for the Testing environment (see
        /// <c>GameServerFactory</c> and <c>appsettings.Testing.json</c>) so hand-built tokens validate.
        /// </summary>
        public const string TestSigningKey = "test-signing-key-for-integration-tests-at-least-32-bytes";

        /// <summary>Creates a pre-selection access token (no selected-player claim) for the user.</summary>
        public static string CreateAccessToken(int userId, params string[] roles)
        {
            return CreateAccessToken(userId, null, roles);
        }

        /// <summary>
        /// Creates an access token for the user, carrying the selected-player claim when
        /// <paramref name="playerId"/> is supplied (a post-selection token) — mirroring what
        /// <c>SelectPlayer</c> issues in production.
        /// </summary>
        public static string CreateAccessToken(int userId, int? playerId, params string[] roles)
        {
            var tokenService = new JwtTokenService(Options.Create(new JwtOptions { SigningKey = TestSigningKey }));
            return tokenService.CreateAccessToken(userId, roles, playerId);
        }

        /// <summary>Attaches a pre-selection bearer token (no selected player) for the user.</summary>
        public static void AddAuthHeader(HttpClient client, int userId, params string[] roles)
        {
            AddAuthHeader(client, userId, null, roles);
        }

        /// <summary>Attaches a bearer token carrying the selected-player claim (a post-selection token).</summary>
        public static void AddAuthHeader(HttpClient client, int userId, int? playerId, params string[] roles)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateAccessToken(userId, playerId, roles));
        }
    }
}
