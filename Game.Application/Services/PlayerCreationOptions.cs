namespace Game.Application.Services
{
    /// <summary>
    /// Configuration for player (character) creation, bound from the "PlayerCreation" section. Like the
    /// login backoff, it carries a safe positive default so an unconfigured deployment still enforces a cap;
    /// a deployment only overrides it to tune the limit. Validated as positive on start so a misconfigured
    /// zero/negative fails fast rather than silently disabling the anti-cheat cap.
    /// </summary>
    public class PlayerCreationOptions
    {
        /// <summary>The configuration section this options class binds from.</summary>
        public const string SectionName = "PlayerCreation";

        /// <summary>The maximum number of characters a single account may own.</summary>
        public int MaxPlayersPerAccount { get; set; } = 6;
    }
}
