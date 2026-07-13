using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess;
using Contracts = Game.Abstractions.Contracts;

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
        /// projection the client and Workbench receive. Mirrors <see cref="ContentExporter"/>'s set list; tags
        /// are the one set not held in a reference cache, so they're materialized from the DB read stream like
        /// the exporter does.</summary>
        private async Task<ContentGraph> BuildGraphAsync(CancellationToken cancellationToken)
        {
            var tags = new List<Contracts.Tag>();
            await foreach (var tag in _tags.All().WithCancellation(cancellationToken))
            {
                tags.Add(tag);
            }

            return new ContentGraph(
                Skills: _skills.AllSkills(),
                Tags: tags,
                Items: _items.All(),
                ItemMods: _itemMods.All(),
                Enemies: _enemies.All(),
                Zones: _zones.All(),
                Challenges: _challenges.All().Select(ChallengeContractMapper.ToContract).ToList(),
                Classes: _classes.All(),
                Paths: _proficiencies.AllPaths(),
                Proficiencies: _proficiencies.AllProficiencies(),
                SkillRecipes: _skillRecipes.AllSkillRecipes(),
                Lessons: _lessons.AllLessons());
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
