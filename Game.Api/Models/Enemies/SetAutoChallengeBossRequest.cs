namespace Game.Api.Models.Enemies
{
    /// <summary>
    /// Syncs the player's active idle-loop mode for the offline-rewards flow. <see cref="Enabled"/> true
    /// enters boss mode, auto-challenging <see cref="ZoneId"/>'s dedicated boss; false returns the loop to
    /// idle-farming (the zone is then ignored).
    /// </summary>
    public class SetAutoChallengeBossRequest
    {
        /// <summary>Whether the player's loop is auto-challenging a boss (true) or idle-farming (false).</summary>
        public bool Enabled { get; set; }

        /// <summary>The zone whose dedicated boss to farm. Only meaningful when <see cref="Enabled"/> is true.</summary>
        public int ZoneId { get; set; }
    }
}
