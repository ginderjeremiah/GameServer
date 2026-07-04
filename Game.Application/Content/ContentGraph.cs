using Contracts = Game.Abstractions.Contracts;

namespace Game.Application.Content
{
    /// <summary>
    /// An in-memory snapshot of the whole static reference-data graph — the 11 exported sets — that the
    /// <see cref="IProgressionGraphChecker">progression-graph lint</see> walks. Deliberately source-agnostic:
    /// it can be built from the committed <c>content/*.json</c> export (the CI lint) or from the live reference
    /// caches (a future admin "Content Health" view), so the same checker powers both (spike #1390, decision 5).
    /// </summary>
    public sealed record ContentGraph(
        IReadOnlyList<Contracts.Skill> Skills,
        IReadOnlyList<Contracts.Item> Items,
        IReadOnlyList<Contracts.ItemMod> ItemMods,
        IReadOnlyList<Contracts.Enemy> Enemies,
        IReadOnlyList<Contracts.Zone> Zones,
        IReadOnlyList<Contracts.Challenge> Challenges,
        IReadOnlyList<Contracts.Class> Classes,
        IReadOnlyList<Contracts.Path> Paths,
        IReadOnlyList<Contracts.Proficiency> Proficiencies,
        IReadOnlyList<Contracts.SkillRecipe> SkillRecipes,
        IReadOnlyList<Contracts.Lesson> Lessons);
}
