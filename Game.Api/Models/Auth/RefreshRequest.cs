namespace Game.Api.Models.Auth
{
    /// <summary>
    /// Request body carrying a refresh token, used to mint a new token pair (refresh) or to revoke the
    /// token (logout).
    /// </summary>
    public class RefreshRequest : IModel
    {
        public required string RefreshToken { get; set; }
    }
}
