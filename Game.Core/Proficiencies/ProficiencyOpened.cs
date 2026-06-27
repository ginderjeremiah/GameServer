namespace Game.Core.Proficiencies
{
    /// <summary>
    /// A proficiency a won battle newly opened (spike #982 area D): a within-path next tier revealed by maxing
    /// the tier before it. Notification-only — opening grants no skill (synthesis, spike #1125, provides the
    /// player-driven bootstrap for new lines). Pushed to the client so the tree surfaces the new node immediately.
    /// </summary>
    public record ProficiencyOpened(int ProficiencyId);
}
