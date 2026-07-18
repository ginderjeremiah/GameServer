using Game.Abstractions.DataAccess;

namespace Game.Application.Content
{
    /// <inheritdoc cref="IContentExporter"/>
    internal sealed class ContentExporter : IContentExporter
    {
        private readonly ISkills _skills;
        private readonly ITags _tags;
        private readonly IItems _items;
        private readonly IItemMods _itemMods;
        private readonly IEnemies _enemies;
        private readonly IZones _zones;
        private readonly IChallenges _challenges;
        private readonly IClasses _classes;
        private readonly IProficiencies _proficiencies;
        private readonly ISkillRecipes _skillRecipes;
        private readonly ILessons _lessons;

        public ContentExporter(
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
            ILessons lessons)
        {
            _skills = skills;
            _tags = tags;
            _items = items;
            _itemMods = itemMods;
            _enemies = enemies;
            _zones = zones;
            _challenges = challenges;
            _classes = classes;
            _proficiencies = proficiencies;
            _skillRecipes = skillRecipes;
            _lessons = lessons;
        }

        public async Task<IReadOnlyList<ContentExportFile>> ExportAllAsync(CancellationToken cancellationToken = default)
        {
            var graph = await LiveContentGraphAssembler.BuildAsync(
                _skills, _tags, _items, _itemMods, _enemies, _zones, _challenges, _classes, _proficiencies,
                _skillRecipes, _lessons, cancellationToken);

            // One file per static set, in dependency-ish order (referenced sets first). Tags are referenced by
            // items/mods, so they precede them. Path and Proficiency are distinct reference sets with their own
            // ids, so each gets its own file.
            return
            [
                File("skills.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(graph.Skills))),
                File("tags.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(graph.Tags))),
                File("item-mods.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(graph.ItemMods))),
                File("items.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(graph.Items))),
                File("enemies.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(graph.Enemies))),
                File("challenges.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(graph.Challenges))),
                File("zones.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(graph.Zones))),
                File("classes.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(graph.Classes))),
                File("paths.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(graph.Paths))),
                File("proficiencies.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(graph.Proficiencies))),
                File("skill-recipes.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(graph.SkillRecipes))),
                File("lessons.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(graph.Lessons))),
            ];
        }

        private static ContentExportFile File(string name, string json) => new(name, json);
    }
}
