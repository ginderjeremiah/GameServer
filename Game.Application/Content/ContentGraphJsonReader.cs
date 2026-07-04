using Contracts = Game.Abstractions.Contracts;

namespace Game.Application.Content
{
    /// <summary>
    /// Loads the committed <c>content/*.json</c> export into a <see cref="ContentGraph"/> for the progression
    /// graph lint (#1420). File names mirror the exporter's (<see cref="ContentExporter"/>); each set is
    /// deserialized through the export's own canonical <see cref="JsonSerializerOptions"/>, so the lint reads
    /// exactly the graph the drift guard pins the files to.
    /// </summary>
    internal static class ContentGraphJsonReader
    {
        public static async Task<ContentGraph> ReadFromDirectoryAsync(string contentDirectory, CancellationToken cancellationToken = default)
        {
            return new ContentGraph(
                await ReadAsync<Contracts.Skill>(contentDirectory, "skills.json", cancellationToken),
                await ReadAsync<Contracts.Item>(contentDirectory, "items.json", cancellationToken),
                await ReadAsync<Contracts.ItemMod>(contentDirectory, "item-mods.json", cancellationToken),
                await ReadAsync<Contracts.Enemy>(contentDirectory, "enemies.json", cancellationToken),
                await ReadAsync<Contracts.Zone>(contentDirectory, "zones.json", cancellationToken),
                await ReadAsync<Contracts.Challenge>(contentDirectory, "challenges.json", cancellationToken),
                await ReadAsync<Contracts.Class>(contentDirectory, "classes.json", cancellationToken),
                await ReadAsync<Contracts.Path>(contentDirectory, "paths.json", cancellationToken),
                await ReadAsync<Contracts.Proficiency>(contentDirectory, "proficiencies.json", cancellationToken),
                await ReadAsync<Contracts.SkillRecipe>(contentDirectory, "skill-recipes.json", cancellationToken),
                await ReadAsync<Contracts.Lesson>(contentDirectory, "lessons.json", cancellationToken));
        }

        private static async Task<IReadOnlyList<T>> ReadAsync<T>(string contentDirectory, string fileName, CancellationToken cancellationToken)
        {
            var path = Path.Combine(contentDirectory, fileName);
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return ContentExportSerializer.Deserialize<T>(json);
        }
    }
}
