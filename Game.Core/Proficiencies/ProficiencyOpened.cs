namespace Game.Core.Proficiencies
{
    /// <summary>
    /// A proficiency a won battle newly opened (spike #982 area D) — a within-path next tier revealed by maxing
    /// the tier before it, or a cross-path gateway whose prerequisites all became maxed. Notification-only:
    /// opening grants no skill (the freshly-revealed tier's native, full-pace training skill is re-homed onto
    /// the previous tier's max-level milestone reward — skill synthesis, spike #1125). Pushed to the client so
    /// the tree surfaces the new node immediately.
    /// </summary>
    public record ProficiencyOpened(int ProficiencyId);
}
