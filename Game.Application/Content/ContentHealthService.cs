using Game.Abstractions.Content;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess;

namespace Game.Application.Content
{
    /// <inheritdoc cref="IContentHealthService"/>
    internal sealed class ContentHealthService : IContentHealthService
    {
        private readonly IProgressionGraphChecker _checker;
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

        public ContentHealthService(
            IProgressionGraphChecker checker,
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
            _checker = checker;
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

        public async Task<ContentHealthReport> GetReportAsync(CancellationToken cancellationToken = default)
        {
            return ToReport(_checker.Check(await BuildGraphAsync(cancellationToken)));
        }

        /// <summary>Assembles the whole static graph from the in-memory reference caches — the same published
        /// projection the client and Workbench receive. Shares <see cref="LiveContentGraphAssembler"/> with
        /// <see cref="ContentExporter"/>, which builds the identical graph before canonicalizing and
        /// serializing it.</summary>
        private Task<ContentGraph> BuildGraphAsync(CancellationToken cancellationToken)
        {
            return LiveContentGraphAssembler.BuildAsync(
                _skills, _tags, _items, _itemMods, _enemies, _zones, _challenges, _classes, _proficiencies,
                _skillRecipes, _lessons, cancellationToken);
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
