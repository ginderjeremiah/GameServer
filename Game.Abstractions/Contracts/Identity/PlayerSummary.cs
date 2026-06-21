namespace Game.Abstractions.Contracts.Identity
{
    /// <summary>
    /// A lightweight summary of one of a user's players. Shared by the admin user view and the
    /// login player-selection list (both read off the shared User aggregate): the admin Workbench
    /// surfaces the name/level/last-activity, while the player-select screen also needs the current
    /// zone to show where each character left off.
    /// </summary>
    public class PlayerSummary : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int Level { get; set; }
        public int CurrentZoneId { get; set; }
        public DateTime LastActivity { get; set; }
    }
}
