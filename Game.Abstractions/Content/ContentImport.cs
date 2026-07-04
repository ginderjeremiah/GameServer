using Contracts = Game.Abstractions.Contracts;

namespace Game.Abstractions.Content
{
    /// <summary>
    /// The full static reference-data graph deserialized from the source-controlled content export
    /// (<c>content/*.json</c>, spike #1390) — one list per static set, in the published read-contract shape. It
    /// is the input the content seeder reconstructs a fresh database from, the reverse of the content
    /// exporter's output.
    /// </summary>
    public sealed record ContentImport
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
