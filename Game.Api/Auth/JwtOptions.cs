namespace Game.Api.Auth
{
    /// <summary>
    /// Configuration for issuing and validating JWT access tokens, bound from the "Jwt" configuration
    /// section. The signing key is a secret and must be supplied via configuration
    /// (user secrets / environment variables) — there is no default.
    /// </summary>
    public class JwtOptions
    {
        public string SigningKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = Constants.SERVER_PRINCIPAL;
        public string Audience { get; set; } = Constants.SERVER_PRINCIPAL;
    }
}
