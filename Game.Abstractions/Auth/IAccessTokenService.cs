namespace Game.Abstractions.Auth
{
    /// <summary>
    /// Issues the short-lived signed access tokens handed to a client on login/refresh. Abstracted here
    /// so the application layer can orchestrate token issuance without depending on the concrete JWT
    /// implementation (which lives at the presentation edge alongside the bearer-validation pipeline).
    /// </summary>
    public interface IAccessTokenService
    {
        /// <summary>
        /// Creates a signed access token carrying the given user id and role claims.
        /// </summary>
        string CreateAccessToken(int userId, IReadOnlyList<string> roles);
    }
}
