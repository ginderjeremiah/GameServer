using Game.Abstractions.Content;
using Contracts = Game.Abstractions.Contracts;

namespace Game.Application.Content
{
    /// <inheritdoc cref="IContentImportReader"/>
    internal sealed class ContentImportReader : IContentImportReader
    {
        /// <summary>The content directory shipped alongside the running application. The export files are copied
        /// into the build output (see <c>Game.Api.csproj</c>), so this resolves for both a local run and the
        /// published/containerized app without any environment-specific path logic.</summary>
        public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "content");

        public ContentImport ReadDefault() => Read(DefaultDirectory);

        public ContentImport Read(string directory)
        {
            return new ContentImport
            {
                Skills = ReadSet<Contracts.Skill>(directory, "skills.json"),
                ItemMods = ReadSet<Contracts.ItemMod>(directory, "item-mods.json"),
                Items = ReadSet<Contracts.Item>(directory, "items.json"),
                Enemies = ReadSet<Contracts.Enemy>(directory, "enemies.json"),
                Challenges = ReadSet<Contracts.Challenge>(directory, "challenges.json"),
                Zones = ReadSet<Contracts.Zone>(directory, "zones.json"),
                Classes = ReadSet<Contracts.Class>(directory, "classes.json"),
                Paths = ReadSet<Contracts.Path>(directory, "paths.json"),
                Proficiencies = ReadSet<Contracts.Proficiency>(directory, "proficiencies.json"),
                SkillRecipes = ReadSet<Contracts.SkillRecipe>(directory, "skill-recipes.json"),
            };
        }

        private static IReadOnlyList<T> ReadSet<T>(string directory, string fileName)
        {
            var path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Content export file '{fileName}' was not found under '{directory}'. The seeder expects the " +
                    "full source-controlled export to be present.", path);
            }

            return ContentExportSerializer.Deserialize<T>(File.ReadAllText(path));
        }
    }
}
