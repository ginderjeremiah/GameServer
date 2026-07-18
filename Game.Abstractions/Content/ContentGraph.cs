using Contracts = Game.Abstractions.Contracts;

namespace Game.Abstractions.Content
{
    /// <summary>
    /// The whole static reference-data graph — the 12 exported sets, in the published read-contract shape
    /// (spike #1390). Source-agnostic: the same shape is populated from the committed <c>content/*.json</c>
    /// export (the content seeder's input, and the CI progression-graph lint's input) or from the live
    /// reference caches (the admin Content Health view), so one type serves every consumer (spike #1390,
    /// decision 5).
    /// </summary>
    public sealed record ContentGraph
    {
        public required IReadOnlyList<Contracts.Skill> Skills { get; init; }
        public required IReadOnlyList<Contracts.Tag> Tags { get; init; }
        public required IReadOnlyList<Contracts.ItemMod> ItemMods { get; init; }
        public required IReadOnlyList<Contracts.Item> Items { get; init; }
        public required IReadOnlyList<Contracts.Enemy> Enemies { get; init; }
        public required IReadOnlyList<Contracts.Challenge> Challenges { get; init; }
        public required IReadOnlyList<Contracts.Zone> Zones { get; init; }
        public required IReadOnlyList<Contracts.Class> Classes { get; init; }
        public required IReadOnlyList<Contracts.Path> Paths { get; init; }
        public required IReadOnlyList<Contracts.Proficiency> Proficiencies { get; init; }
        public required IReadOnlyList<Contracts.SkillRecipe> SkillRecipes { get; init; }
        public required IReadOnlyList<Contracts.Lesson> Lessons { get; init; }
    }
}
