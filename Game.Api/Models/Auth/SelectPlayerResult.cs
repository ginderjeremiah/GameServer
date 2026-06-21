using Game.Api.Models.Player;

namespace Game.Api.Models.Auth
{
    /// <summary>
    /// The result of selecting a character: the rotated auth tokens (now carrying the selected player)
    /// plus the loaded player data, so the client can enter the game in a single round-trip.
    /// </summary>
    public class SelectPlayerResult : IModel
    {
        public required AuthTokens Tokens { get; set; }
        public required PlayerData Player { get; set; }
    }
}
