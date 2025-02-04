namespace Game.Core.Players
{
    /// <summary>
    /// Represents the current state of a player.
    /// </summary>
    public class PlayerState
    {
        /// <summary>
        /// The unique identifier of the player.
        /// </summary>
        public int PlayerId { get; set; }

        /// <summary>
        /// The time at which the player will be able to find a new enemy again.
        /// </summary>
        public DateTime EnemyCooldown { get; set; } = DateTime.UnixEpoch;
    }
}
