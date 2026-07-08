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

        /// <summary>When true, a still-in-progress battle is discarded rather than handed back unchanged —
        /// the same override <c>ChallengeBoss</c> always applies. Lets a retreat leave a still-active boss
        /// fight immediately instead of waiting for it to reach a real conclusion (#1690).</summary>
        public bool ForceAbandon { get; set; }
    }
}
