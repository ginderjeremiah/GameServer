namespace Game.Api.Models.Enemies
{
    public class NewEnemyRequest
    {
        public int? NewZoneId { get; set; }

        /// <summary>How long (ms) the client actually simulated the battle this request supersedes, used to
        /// bound the abandon re-simulation accurately. A battle the client never fought — e.g. a
        /// server-prefetched next battle discarded by a zone/build change during the cooldown — reports 0,
        /// so the abandon records no outcome. Null when not reported (the server falls back to wall-clock).</summary>
        public int? ClientBattleMs { get; set; }
    }
}
