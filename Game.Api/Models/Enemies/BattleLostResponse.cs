namespace Game.Api.Models.Enemies
{
    public class BattleLostResponse : IModel
    {
        public double Cooldown { get; set; }

        /// <summary>The next idle battle, prefetched and bundled with the boss loss so the client can begin
        /// it the instant the post-loss cooldown elapses without a separate <c>NewEnemy</c> round-trip. Null
        /// when no next battle was prepared (e.g. a failed loss).</summary>
        public EnemyInstance? NextEnemy { get; set; }

        /// <summary>The authoritative zone the bundled <see cref="NextEnemy"/> runs in. The client only uses
        /// the bundled enemy when this still matches its current zone; null when none was prepared.</summary>
        public int? NextZoneId { get; set; }
    }
}
