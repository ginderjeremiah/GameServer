namespace Game.Api.Models.Auth
{
    /// <summary>
    /// The token pair returned to a client on login or refresh: a short-lived JWT access token (sent
    /// as a bearer token on subsequent requests) and a longer-lived opaque refresh token used to mint
    /// a new pair once the access token expires.
    /// </summary>
    public class AuthTokens : IModel
    {
        public required string AccessToken { get; set; }
        public required string RefreshToken { get; set; }
    }
}
