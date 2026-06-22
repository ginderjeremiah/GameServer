namespace Game.Core.Progress
{
    /// <summary>
    /// A player's progress in a proficiency (a mastery track): its current <see cref="Level"/> and
    /// accumulated <see cref="Xp"/>, keyed by <see cref="ProficiencyId"/> within a player. The absence of a
    /// row is the "never trained" state, so a stored row always carries a genuine level/XP.
    /// </summary>
    public class PlayerProficiency
    {
        public required int ProficiencyId { get; set; }
        public required int Level { get; set; }
        public required decimal Xp { get; set; }
    }
}
