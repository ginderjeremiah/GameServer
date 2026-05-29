using Game.Api;
using Game.Api.Auth;
using Game.Core;

namespace Game.TestInfrastructure.Helpers
{
    public static class TestAuthHelper
    {
        public const string TestPepper = "test-pepper-value-for-integration-tests";

        public static void EnsurePepperSet()
        {
            Hashing.SetPepper(TestPepper);
        }

        public static string CreateAuthTokenString(int userId)
        {
            EnsurePepperSet();
            var claims = new AuthTokenClaims(userId, DateTime.UtcNow.Add(Constants.TOKEN_LIFETIME));
            var token = new AuthToken(claims);
            return token.ToString();
        }

        public static void AddAuthCookie(HttpClient client, int userId)
        {
            var tokenString = CreateAuthTokenString(userId);
            client.DefaultRequestHeaders.Add("Cookie", $"{Constants.TOKEN_NAME}={tokenString}");
        }
    }
}
