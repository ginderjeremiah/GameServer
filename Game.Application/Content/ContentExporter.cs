using Game.Abstractions.DataAccess;

namespace Game.Application.Content
{
    /// <inheritdoc cref="IContentExporter"/>
    internal sealed class ContentExporter : IContentExporter
    {
        private readonly ISkills _skills;
        private readonly IItems _items;
        private readonly IItemMods _itemMods;
        private readonly IEnemies _enemies;
        private readonly IZones _zones;
        private readonly IChallenges _challenges;
        private readonly IClasses _classes;
        private readonly IProficiencies _proficiencies;
        private readonly ISkillRecipes _skillRecipes;

        public ContentExporter(
            ISkills skills,
            IItems items,
            IItemMods itemMods,
            IEnemies enemies,
            IZones zones,
            IChallenges challenges,
            IClasses classes,
            IProficiencies proficiencies,
            ISkillRecipes skillRecipes)
        {
            _skills = skills;
            _items = items;
            _itemMods = itemMods;
            _enemies = enemies;
            _zones = zones;
            _challenges = challenges;
            _classes = classes;
            _proficiencies = proficiencies;
            _skillRecipes = skillRecipes;
        }

        public IReadOnlyList<ContentExportFile> ExportAll()
        {
            // One file per static set, in dependency-ish order (referenced sets first). Path and Proficiency
            // are distinct reference sets with their own ids, so each gets its own file.
            return
            [
                File("skills.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_skills.AllSkills()))),
                File("item-mods.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_itemMods.All()))),
                File("items.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_items.All()))),
                File("enemies.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_enemies.All()))),
                File("challenges.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_challenges.All().Select(ChallengeContractMapper.ToContract)))),
                File("zones.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_zones.All()))),
                File("classes.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_classes.All()))),
                File("paths.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_proficiencies.AllPaths()))),
                File("proficiencies.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_proficiencies.AllProficiencies()))),
                File("skill-recipes.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_skillRecipes.AllSkillRecipes()))),
            ];
        }

        private static ContentExportFile File(string name, string json) => new(name, json);
    }
}
