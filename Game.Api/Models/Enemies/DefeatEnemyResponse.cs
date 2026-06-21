namespace Game.Api.Models.Enemies
{
    public class DefeatEnemyResponse : IModel
    {
        public double Cooldown { get; set; }
        public DefeatRewards? Rewards { get; set; }

        /// <summary>The next idle battle, prefetched and bundled with the victory so the client can begin it
        /// the instant the post-battle cooldown elapses without a separate <c>NewEnemy</c> round-trip. Set
        /// only for an idle victory (the boss-victory path paces itself); null for a boss victory or when no
        /// next battle was prepared.</summary>
        public EnemyInstance? NextEnemy { get; set; }

        /// <summary>The authoritative zone the bundled <see cref="NextEnemy"/> runs in (the server may have
        /// relocated the player out of a now-unplayable zone). The client only uses the bundled enemy when
        /// this still matches its current zone; null when no next battle was prepared.</summary>
        public int? NextZoneId { get; set; }
    }
}
