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

        public static string CreateAccessToken(int userId, params string[] roles)
        {
            var tokenService = new JwtTokenService(Options.Create(new JwtOptions { SigningKey = TestSigningKey }));
            return tokenService.CreateAccessToken(userId, roles);
        }

        public static void AddAuthHeader(HttpClient client, int userId, params string[] roles)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateAccessToken(userId, roles));
        }
    }
}
