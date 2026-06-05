using Game.Api.Models.Player;

namespace Game.Api.Models.Auth
{
    /// <summary>
    /// The result of a successful login: the issued auth tokens plus the player's data, so the client
    /// can both authenticate subsequent requests and enter the game in a single round-trip.
    /// </summary>
    public class LoginResult : IModel
    {
        public required AuthTokens Tokens { get; set; }
        public required PlayerData Player { get; set; }
    }
}
