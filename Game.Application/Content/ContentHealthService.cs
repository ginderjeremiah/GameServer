using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess;

namespace Game.Application.Content
{
    /// <inheritdoc cref="IContentHealthService"/>
    internal sealed class ContentHealthService : IContentHealthService
    {
        private readonly IProgressionGraphChecker _checker;
        private readonly ISkills _skills;
        private readonly IItems _items;
        private readonly IItemMods _itemMods;
        private readonly IEnemies _enemies;
        private readonly IZones _zones;
        private readonly IChallenges _challenges;
        private readonly IClasses _classes;
        private readonly IProficiencies _proficiencies;
        private readonly ISkillRecipes _skillRecipes;

        public ContentHealthService(
            IProgressionGraphChecker checker,
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
            _checker = checker;
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

        public ContentHealthReport GetReport()
        {
            return ToReport(_checker.Check(BuildGraph()));
        }

        /// <summary>Assembles the whole static graph from the in-memory reference caches — the same published
        /// projection the client and Workbench receive. Mirrors <see cref="ContentExporter"/>'s set list, minus
        /// tags (the lint's <see cref="ContentGraph"/> resolves tag joins through items/mods, not a tag set).</summary>
        private ContentGraph BuildGraph()
        {
            return new ContentGraph(
                Skills: _skills.AllSkills(),
                Items: _items.All(),
                ItemMods: _itemMods.All(),
                Enemies: _enemies.All(),
                Zones: _zones.All(),
                Challenges: _challenges.All().Select(ChallengeContractMapper.ToContract).ToList(),
                Classes: _classes.All(),
                Paths: _proficiencies.AllPaths(),
                Proficiencies: _proficiencies.AllProficiencies(),
                SkillRecipes: _skillRecipes.AllSkillRecipes());
        }

        /// <summary>Projects the checker's domain findings onto the admin report contract and tallies the
        /// per-severity counts. Pure — the meaningful logic seam, unit-tested independently of the caches.</summary>
        internal static ContentHealthReport ToReport(IReadOnlyList<ContentGraphFinding> findings)
        {
            var mapped = findings
                .Select(finding => new ContentHealthFinding
                {
                    Severity = finding.Severity == ContentGraphSeverity.Error
                        ? EContentHealthSeverity.Error
                        : EContentHealthSeverity.Warning,
                    Check = finding.Check,
                    EntityKind = finding.EntityKind,
                    EntityId = finding.EntityId,
                    Message = finding.Message,
                })
                .ToList();

            return new ContentHealthReport
            {
                ErrorCount = mapped.Count(finding => finding.Severity == EContentHealthSeverity.Error),
                WarningCount = mapped.Count(finding => finding.Severity == EContentHealthSeverity.Warning),
                Findings = mapped,
            };
        }
    }
}
