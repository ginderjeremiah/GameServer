namespace Game.Api.Models.Enemies
{
    public class NewEnemyModel : IModel
    {
        public double? Cooldown { get; set; }
        public EnemyInstance? EnemyInstance { get; set; }

        /// <summary>The authoritative zone the spawned battle runs in. Usually the player's current zone,
        /// but the server may have relocated them out of a now-unplayable zone (retired, or emptied of
        /// spawnable enemies), so the client adopts this to stay in sync. Null when no battle started
        /// (cooldown/error responses).</summary>
        public int? ZoneId { get; set; }
    }
}
