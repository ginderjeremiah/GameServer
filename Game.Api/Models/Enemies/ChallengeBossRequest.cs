namespace Game.Api.Models.Enemies
{
    public class ChallengeBossRequest
    {
        /// <summary>The zone whose dedicated boss to challenge. Defaults to the player's current zone when
        /// omitted.</summary>
        public int? ZoneId { get; set; }

        /// <summary>How long (ms) the client actually simulated the battle this challenge supersedes, used to
        /// bound the abandon re-simulation accurately. A battle the client never fought — e.g. challenging
        /// during the post-battle cooldown, before the prefetched next battle was engaged — reports 0, so the
        /// abandon records no outcome. Null when not reported (the server falls back to wall-clock).</summary>
        public int? ClientBattleMs { get; set; }
    }
}
