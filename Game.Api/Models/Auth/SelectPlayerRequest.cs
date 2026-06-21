namespace Game.Api.Models.Auth
{
    /// <summary>
    /// Selects which of the account's characters to enter as. Carries the pre-selection refresh token so
    /// the server can rotate the token pair (single use) into one anchored to the chosen player.
    /// </summary>
    public class SelectPlayerRequest : IModel
    {
        public int PlayerId { get; set; }
        public required string RefreshToken { get; set; }
    }
}
