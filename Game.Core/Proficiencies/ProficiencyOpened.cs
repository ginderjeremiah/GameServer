namespace Game.Core.Proficiencies
{
    /// <summary>
    /// A proficiency a won battle newly opened (spike #982 area D) — a within-path next tier revealed by maxing
    /// the tier before it, or a cross-path gateway whose prerequisites all became maxed. <see cref="SeedSkillId"/>
    /// is the native skill granted on open so the freshly-revealed tier has a full-pace training vehicle
    /// (decision 8); it is null for a node seeded by a world skill (an item/starter contribution) instead.
    /// Pushed to the client so the tree surfaces the new node immediately.
    /// </summary>
    public record ProficiencyOpened(int ProficiencyId, int? SeedSkillId);
}
