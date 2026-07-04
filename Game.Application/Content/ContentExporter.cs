using Game.Abstractions.DataAccess;
using Contracts = Game.Abstractions.Contracts;

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
            // Tags are the one set not held in an in-memory reference cache, so materialize them from the
            // database read stream before assembling the (otherwise synchronous) export.
            var tags = new List<Contracts.Tag>();
            await foreach (var tag in _tags.All().WithCancellation(cancellationToken))
            {
                tags.Add(tag);
            }

            // One file per static set, in dependency-ish order (referenced sets first). Tags are referenced by
            // items/mods, so they precede them. Path and Proficiency are distinct reference sets with their own
            // ids, so each gets its own file.
            return
            [
                File("skills.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_skills.AllSkills()))),
                File("tags.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(tags))),
                File("item-mods.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_itemMods.All()))),
                File("items.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_items.All()))),
                File("enemies.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_enemies.All()))),
                File("challenges.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_challenges.All().Select(ChallengeContractMapper.ToContract)))),
                File("zones.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_zones.All()))),
                File("classes.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_classes.All()))),
                File("paths.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_proficiencies.AllPaths()))),
                File("proficiencies.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_proficiencies.AllProficiencies()))),
                File("skill-recipes.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_skillRecipes.AllSkillRecipes()))),
                File("lessons.json", ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(_lessons.AllLessons()))),
            ];
        }

        private static ContentExportFile File(string name, string json) => new(name, json);
    }
}
