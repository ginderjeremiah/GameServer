using Game.Abstractions.Content;
using Game.Abstractions.DataAccess;
using Contracts = Game.Abstractions.Contracts;

namespace Game.Application.Content
{
    /// <summary>
    /// Assembles a <see cref="ContentGraph"/> straight from the live in-memory reference caches — the same
    /// published projection the client and Workbench receive. Shared by <see cref="ContentExporter"/> (which
    /// additionally canonicalizes and serializes each set) and <see cref="ContentHealthService"/> (which checks
    /// the graph as-is), so the 11 cache accessor calls plus the tags-from-DB materialization exist in exactly
    /// one place.
    /// </summary>
    internal static class LiveContentGraphAssembler
    {
        public static async Task<ContentGraph> BuildAsync(
            ISkills skills,
            ITags tags,
            IItems items,
            IItemMods itemMods,
            IEnemies enemies,
            IZones zones,
            IChallenges challenges,
            IClasses classes,
            IProficiencies proficiencies,
            ISkillRecipes skillRecipes,
            ILessons lessons,
            CancellationToken cancellationToken)
        {
            // Tags are the one set not held in an in-memory reference cache, so materialize them from the
            // database read stream.
            var tagList = new List<Contracts.Tag>();
            await foreach (var tag in tags.All().WithCancellation(cancellationToken))
            {
                tagList.Add(tag);
            }

            return new ContentGraph
            {
                Skills = skills.AllSkills(),
                Tags = tagList,
                ItemMods = itemMods.All(),
                Items = items.All(),
                Enemies = enemies.All(),
                Challenges = challenges.All().Select(ChallengeContractMapper.ToContract).ToList(),
                Zones = zones.All(),
                Classes = classes.All(),
                Paths = proficiencies.AllPaths(),
                Proficiencies = proficiencies.AllProficiencies(),
                SkillRecipes = skillRecipes.AllSkillRecipes(),
                Lessons = lessons.AllLessons(),
            };
        }
    }
}
