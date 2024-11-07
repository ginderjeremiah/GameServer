using Game.Core;
using System.Diagnostics.CodeAnalysis;

namespace Game.Api.Auth
{
    public class AuthToken
    {
        private readonly string _tokenClaims;
        private readonly string _tokenSignature;

        public AuthTokenClaims Claims { get; set; }
        public string Signature { get; private set; }

        private AuthToken(AuthTokenClaims claims, string tokenClaims, string tokenSignature)
        {
            Claims = claims;
            _tokenClaims = tokenClaims;
            Signature = tokenSignature.FromBase64();
            _tokenSignature = tokenSignature;
        }

        public AuthToken(AuthTokenClaims claims, string salt)
        {
            Claims = claims;
            _tokenClaims = Claims.Serialize().ToBase64();
            Signature = GenerateSignature(salt);
            _tokenSignature = Signature.ToBase64();
        }

        public static bool TryParseToken(string? tokenString, [NotNullWhen(true)] out AuthToken? token)
        {
            var tokenParts = tokenString?.Split('.');
            if (tokenParts is not null && tokenParts.Length == 2 && TryDeserializeClaims(tokenParts[0], out var claims) && claims.Exp >= DateTime.UtcNow)
            {
                token = new AuthToken(claims, tokenParts[0], tokenParts[1]);
            }
            else
            {
                token = null;
            }

            return token is not null;
        }

        public bool IsValid(string salt)
        {
            return GenerateSignature(salt) == Signature;
        }

        public override string ToString()
        {
            return $"{_tokenClaims}.{_tokenSignature}";
        }

        private static bool TryDeserializeClaims(string claimsString, [NotNullWhen(true)] out AuthTokenClaims? claims)
        {
            try
            {
                claims = claimsString.FromBase64().Deserialize<AuthTokenClaims>();
            }
            catch
            {
                claims = null;
            }

            return claims is not null;
        }

        private string GenerateSignature(string salt)
        {
            return _tokenClaims.Hash(salt);
        }
    }

    public class AuthTokenClaims
    {
        public string Iss { get; }
        public int Sub { get; }
        public string Aud { get; }
        public DateTime Exp { get; }

        public AuthTokenClaims(int sub, DateTime exp = default, string iss = Constants.SERVER_PRINCIPAL, string aud = Constants.SERVER_PRINCIPAL)
        {
            Sub = sub;
            Exp = exp == default ? DateTime.UtcNow : exp;
            Iss = iss;
            Aud = aud;
        }
    }
}
