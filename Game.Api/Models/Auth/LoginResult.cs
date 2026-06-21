using Game.Abstractions.Contracts.Identity;

namespace Game.Api.Models.Auth
{
    /// <summary>
    /// The result of a successful login: the issued (pre-selection) auth tokens plus a summary of each
    /// character on the account, so the client can authenticate subsequent requests and present the
    /// player-selection step. The selected character is loaded and bound by the follow-up
    /// <c>Login/SelectPlayer</c> call, which rotates the tokens to carry the chosen player.
    /// </summary>
    public class LoginResult : IModel
    {
        public required AuthTokens Tokens { get; set; }
        public required List<PlayerSummary> PlayerSummaries { get; set; }
    }
}
