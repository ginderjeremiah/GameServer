using Game.Core.Battle;
using Game.Core.Proficiencies;
using Xunit;
using CorePath = Game.Core.Proficiencies.Path;

namespace Game.Core.Tests.Proficiencies
{
    /// <summary>
    /// The pure accrual math extracted from the application layer's <c>ProficiencyRewardService</c> into
    /// <c>Game.Core</c> (#1602): frontier routing, XP claim/leveling, and the milestone/open triggers, driven
    /// entirely through the injected <see cref="ProficiencyCatalog"/> and read/write funcs rather than a real
    /// <c>PlayerProgress</c> aggregate or <c>IProficiencies</c>. Classical-style unit tests (no test doubles) —
    /// hand-built <see cref="Proficiency"/>/<see cref="Path"/> reference objects and a small in-memory progress
    /// dictionary stand in for the catalog/aggregate a real caller would supply. DB-backed wiring (skill
    /// granting, notify, live event raising) is covered separately by
    /// <c>Game.Application.Tests.Services.ProficiencyRewardServiceTests</c>.
    /// </summary>
    public class ProficiencyAccrualTests
    {
        private const double FiredDamage = 100.0;

        [Fact]
        public void Accrue_NonPositiveRatingDenominator_YieldsNoResults()
        {
            var (catalog, progress) = SingleTierSetup(maxLevel: 10, baseXp: 1m);

            var accrual = progress.Accrue(catalog, FireStats(), ratingDenominator: 0);

            Assert.Empty(accrual.Results);
            Assert.Empty(accrual.Opened);
        }

        [Fact]
        public void Accrue_NoTrainedActivity_YieldsNoResults()
        {
            var (catalog, progress) = SingleTierSetup(maxLevel: 10, baseXp: 1m);

            // No damage dealt this battle — nothing trains any path.
            var accrual = progress.Accrue(catalog, new BattleStats(), ratingDenominator: FiredDamage);

            Assert.Empty(accrual.Results);
        }

        [Fact]
        public void Accrue_LevelsTheFrontierTier_AndWritesThroughSetProgress()
        {
            var (catalog, progress) = SingleTierSetup(proficiencyId: 5, maxLevel: 10, baseXp: 5m, xpGrowth: 1m);

            // Full pie against a matched rating (activity == ratingDenominator) reaches level 5 over a flat
            // cost-5 curve: pie (ServerGameConstants.ProficiencyXpPerVictory, 29.247) / 5 per level ≈ 5 levels.
            var accrual = progress.Accrue(catalog, FireStats(), ratingDenominator: FiredDamage);

            var result = Assert.Single(accrual.Results);
            Assert.Equal(5, result.ProficiencyId);
            Assert.Equal(result.NewLevel, progress.LevelOf(5));
            Assert.Equal(result.NewXp, progress.XpOf(5));
            Assert.True(result.NewLevel > 0, "Expected the full pie to cross at least one level.");
        }

        [Fact]
        public void Accrue_TrivialActivity_RoundsToZero_AndBanksNothing()
        {
            // A slice whose claim rounds to 0 at the persisted XP scale (numeric(18,3)) is skipped outright —
            // no result, no write — rather than persisting an information-free zero-gain row.
            var (catalog, progress) = SingleTierSetup(maxLevel: 10, baseXp: 1_000_000m);

            var accrual = progress.Accrue(catalog, FireStats(activity: 0.0001), ratingDenominator: 1_000_000_000);

            Assert.Empty(accrual.Results);
        }

        [Fact]
        public void Accrue_CrossingAMilestone_ReportsTheGrantedSkillId_WithoutApplyingAnySideEffect()
        {
            // The milestone reward skill is reported only (GrantedSkillIds) — the pure accrual has no
            // Player/ISkills dependency, so applying the grant is entirely the caller's job.
            var proficiency = MakeProficiency(
                id: 5, pathId: 5, maxLevel: 10, baseXp: 5m, xpGrowth: 1m,
                levels: [new ProficiencyLevel { Level = 3, Modifiers = [], RewardSkillId = 42 }]);
            var (catalog, progress) = Setup((proficiency, MakePath(5, EActivityKey.Physical, proficiency)));

            var accrual = progress.Accrue(catalog, FireStats(), ratingDenominator: FiredDamage);

            var result = Assert.Single(accrual.Results);
            Assert.True(result.NewLevel >= 3, "Expected the full pie to cross the level-3 milestone.");
            Assert.Equal([42], result.GrantedSkillIds);
            Assert.Contains(3, result.MilestonesCrossed);
        }

        [Fact]
        public void Accrue_BonusOnlyMilestone_ReportsTheMilestone_ButGrantsNoSkill()
        {
            var proficiency = MakeProficiency(
                id: 5, pathId: 5, maxLevel: 10, baseXp: 5m, xpGrowth: 1m,
                levels: [new ProficiencyLevel { Level = 3, Modifiers = [], RewardSkillId = null }]);
            var (catalog, progress) = Setup((proficiency, MakePath(5, EActivityKey.Physical, proficiency)));

            var accrual = progress.Accrue(catalog, FireStats(), ratingDenominator: FiredDamage);

            var result = Assert.Single(accrual.Results);
            Assert.Contains(3, result.MilestonesCrossed);
            Assert.Empty(result.GrantedSkillIds);
        }

        [Fact]
        public void Accrue_MaxingATier_OpensTheNextTierInItsPath_WithoutGrantingASkill()
        {
            var tier0 = MakeProficiency(id: 1, pathId: 1, pathOrdinal: 0, maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var tier1 = MakeProficiency(id: 2, pathId: 1, pathOrdinal: 1, maxLevel: 10, baseXp: 100m);
            var path = new CorePath
            {
                Id = 1,
                ActivityKey = EActivityKey.Physical,
                Tiers = [new PathTier(tier0.Id, Ordinal: 0, MaxLevel: tier0.MaxLevel), new PathTier(tier1.Id, Ordinal: 1, MaxLevel: tier1.MaxLevel)],
            };
            var (catalog, progress) = Setup((tier0, path), (tier1, path));

            var accrual = progress.Accrue(catalog, FireStats(), ratingDenominator: FiredDamage);

            Assert.Contains(accrual.Opened, o => o.ProficiencyId == tier1.Id);
            // Opening is notification-only: tier1 has not itself accrued (it wasn't the frontier this battle).
            Assert.DoesNotContain(accrual.Results, r => r.ProficiencyId == tier1.Id);
        }

        [Fact]
        public void Accrue_MaxingTheLastPrerequisite_OpensTheGatedGateway()
        {
            var prereqA = MakeProficiency(id: 1, pathId: 1, maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var prereqB = MakeProficiency(id: 2, pathId: 2, maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var gateway = MakeProficiency(id: 3, pathId: 3, maxLevel: 10, baseXp: 100m, prerequisiteIds: [1, 2]);
            var pathA = MakePath(1, EActivityKey.Physical, prereqA);
            var pathB = MakePath(2, EActivityKey.Fire, prereqB);
            var pathGateway = MakePath(3, EActivityKey.Water, gateway);

            var (catalog, progress) = Setup((prereqA, pathA), (prereqB, pathB), (gateway, pathGateway));
            // Prerequisite A is already maxed; this battle's activity maxes prerequisite B.
            progress.Seed(prereqA.Id, level: 1, xp: 0m);

            var stats = new BattleStats();
            stats.AddTypedDamageDealt(EDamageType.Fire, FiredDamage);
            var accrual = progress.Accrue(catalog, stats, ratingDenominator: FiredDamage);

            Assert.Contains(accrual.Opened, o => o.ProficiencyId == gateway.Id);
        }

        [Fact]
        public void Accrue_MaxingOnePrerequisite_DoesNotOpenAGatewayStillMissingAnother()
        {
            var prereqA = MakeProficiency(id: 1, pathId: 1, maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var prereqB = MakeProficiency(id: 2, pathId: 2, maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var gateway = MakeProficiency(id: 3, pathId: 3, maxLevel: 10, baseXp: 100m, prerequisiteIds: [1, 2]);
            var pathA = MakePath(1, EActivityKey.Physical, prereqA);
            var pathB = MakePath(2, EActivityKey.Fire, prereqB);
            var pathGateway = MakePath(3, EActivityKey.Water, gateway);
            var (catalog, progress) = Setup((prereqA, pathA), (prereqB, pathB), (gateway, pathGateway));

            var stats = new BattleStats();
            stats.AddTypedDamageDealt(EDamageType.Fire, FiredDamage);
            var accrual = progress.Accrue(catalog, stats, ratingDenominator: FiredDamage);

            Assert.DoesNotContain(accrual.Opened, o => o.ProficiencyId == gateway.Id);
        }

        [Fact]
        public void Accrue_LockedUmbrella_AccruesNoLeafActivity_UntilItsPrerequisiteIsMaxed()
        {
            // A leaf Fire tier and an "Elemental" umbrella whose root tier is a cross-path gateway. Fire rolls
            // up into both the Fire and Elemental offense keys, but the umbrella is still locked, so the leaf
            // activity must not bank XP onto it (#1411).
            var fireTier = MakeProficiency(id: 1, pathId: 1, maxLevel: 10, baseXp: 100m);
            var prereq = MakeProficiency(id: 2, pathId: 2, maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var elemental = MakeProficiency(id: 3, pathId: 3, maxLevel: 10, baseXp: 100m, prerequisiteIds: [2]);
            var (catalog, progress) = Setup(
                (fireTier, MakePath(1, EActivityKey.Fire, fireTier)),
                (prereq, MakePath(2, EActivityKey.Physical, prereq)),
                (elemental, MakePath(3, EActivityKey.Elemental, elemental)));

            var stats = new BattleStats();
            stats.AddTypedDamageDealt(EDamageType.Fire, FiredDamage);
            var accrual = progress.Accrue(catalog, stats, ratingDenominator: FiredDamage);

            Assert.Contains(accrual.Results, r => r.ProficiencyId == fireTier.Id);
            Assert.DoesNotContain(accrual.Results, r => r.ProficiencyId == elemental.Id);
        }

        [Fact]
        public void Accrue_UnlockedUmbrella_AccruesFromLeafActivity_OnceItsPrerequisiteIsMaxed()
        {
            var fireTier = MakeProficiency(id: 1, pathId: 1, maxLevel: 10, baseXp: 100m);
            var prereq = MakeProficiency(id: 2, pathId: 2, maxLevel: 1, baseXp: 1m, xpGrowth: 1m);
            var elemental = MakeProficiency(id: 3, pathId: 3, maxLevel: 10, baseXp: 100m, prerequisiteIds: [2]);
            var (catalog, progress) = Setup(
                (fireTier, MakePath(1, EActivityKey.Fire, fireTier)),
                (prereq, MakePath(2, EActivityKey.Physical, prereq)),
                (elemental, MakePath(3, EActivityKey.Elemental, elemental)));
            progress.Seed(prereq.Id, level: 1, xp: 0m);

            var stats = new BattleStats();
            stats.AddTypedDamageDealt(EDamageType.Fire, FiredDamage);
            var accrual = progress.Accrue(catalog, stats, ratingDenominator: FiredDamage);

            Assert.Contains(accrual.Results, r => r.ProficiencyId == fireTier.Id);
            Assert.Contains(accrual.Results, r => r.ProficiencyId == elemental.Id);
        }

        [Fact]
        public void Accrue_ResistKey_TrainsFromExposure_SplitByMitigation()
        {
            // The resist book trains off exposure (not offense damage dealt), split into its mitigated and
            // unmitigated portions (#1454) — both weighted positively, so any incoming physical damage this
            // battle trains a PhysicalResist-keyed path even though the player dealt no damage at all.
            var resistTier = MakeProficiency(id: 1, pathId: 1, maxLevel: 10, baseXp: 5m, xpGrowth: 1m);
            var (catalog, progress) = Setup((resistTier, MakePath(1, EActivityKey.PhysicalResist, resistTier)));

            var stats = new BattleStats();
            stats.AddTypedDamageExposure(EDamageType.Physical, FiredDamage);

            var accrual = progress.Accrue(catalog, stats, ratingDenominator: FiredDamage);

            var result = Assert.Single(accrual.Results);
            Assert.Equal(resistTier.Id, result.ProficiencyId);
        }

        [Fact]
        public void Accrue_APathAbsentFromTheCatalog_NeverAccrues()
        {
            // A retired path is simply absent from PathsForActivityKey (the reverse index the offline
            // simulator/live adapter build from IProficiencies excludes it) — the pure accrual has no separate
            // "retired" concept, it just never sees a path it's not handed.
            var catalog = new ProficiencyCatalog(
                _ => throw new InvalidOperationException("unexpected"),
                _ => throw new InvalidOperationException("unexpected"),
                _ => [],
                _ => []);
            var progress = new FakeProgress();

            var accrual = progress.Accrue(catalog, FireStats(), ratingDenominator: FiredDamage);

            Assert.Empty(accrual.Results);
        }

        // ── Test plumbing ────────────────────────────────────────────────────

        private static BattleStats FireStats(double activity = FiredDamage)
        {
            var stats = new BattleStats();
            stats.AddTypedDamageDealt(EDamageType.Physical, activity);
            return stats;
        }

        private static Proficiency MakeProficiency(
            int id, int pathId, int maxLevel, decimal baseXp, decimal xpGrowth = 1m, int pathOrdinal = 0,
            IReadOnlyList<int>? prerequisiteIds = null, IReadOnlyList<ProficiencyLevel>? levels = null) => new()
            {
                Id = id,
                Name = $"Proficiency {id}",
                Description = string.Empty,
                PathId = pathId,
                PathOrdinal = pathOrdinal,
                MaxLevel = maxLevel,
                BaseXp = (double)baseXp,
                XpGrowth = (double)xpGrowth,
                PrerequisiteIds = prerequisiteIds ?? [],
                Levels = levels ?? [],
            };

        private static CorePath MakePath(int pathId, EActivityKey activityKey, Proficiency tier) => new()
        {
            Id = pathId,
            ActivityKey = activityKey,
            Tiers = [new PathTier(tier.Id, tier.PathOrdinal, tier.MaxLevel)],
        };

        private static (ProficiencyCatalog Catalog, FakeProgress Progress) SingleTierSetup(
            int proficiencyId = 1, int maxLevel = 10, decimal baseXp = 5m, decimal xpGrowth = 1m)
        {
            var proficiency = MakeProficiency(proficiencyId, pathId: proficiencyId, maxLevel: maxLevel, baseXp: baseXp, xpGrowth: xpGrowth);
            return Setup((proficiency, MakePath(proficiencyId, EActivityKey.Physical, proficiency)));
        }

        private static (ProficiencyCatalog Catalog, FakeProgress Progress) Setup(params (Proficiency Proficiency, CorePath Path)[] entries)
        {
            var proficienciesById = entries.ToDictionary(e => e.Proficiency.Id, e => e.Proficiency);
            var pathsById = entries.Select(e => e.Path).DistinctBy(p => p.Id).ToDictionary(p => p.Id);
            var pathsByActivityKey = entries
                .Select(e => e.Path)
                .DistinctBy(p => p.Id)
                .GroupBy(p => p.ActivityKey)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<CorePath>)g.ToList());
            var dependentsOf = entries
                .SelectMany(e => e.Proficiency.PrerequisiteIds.Select(prereqId => (prereqId, e.Proficiency.Id)))
                .GroupBy(pair => pair.prereqId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(pair => pair.Item2).ToList());

            var catalog = new ProficiencyCatalog(
                id => proficienciesById[id],
                id => pathsById[id],
                key => pathsByActivityKey.TryGetValue(key, out var paths) ? paths : [],
                id => dependentsOf.TryGetValue(id, out var dependents) ? dependents : []);

            return (catalog, new FakeProgress());
        }

        /// <summary>A minimal in-memory (level, xp)-by-id store standing in for <c>PlayerProgress</c> — the
        /// exact read/write shape <see cref="ProficiencyAccrual.Accrue"/> takes.</summary>
        private class FakeProgress
        {
            private readonly Dictionary<int, (int Level, decimal Xp)> _byId = [];

            public void Seed(int proficiencyId, int level, decimal xp) => _byId[proficiencyId] = (level, xp);

            public int LevelOf(int proficiencyId) => _byId.TryGetValue(proficiencyId, out var p) ? p.Level : 0;
            public decimal XpOf(int proficiencyId) => _byId.TryGetValue(proficiencyId, out var p) ? p.Xp : 0m;

            public ProficiencyAccrualResult Accrue(ProficiencyCatalog catalog, BattleStats stats, double ratingDenominator) =>
                ProficiencyAccrual.Accrue(catalog, stats, ratingDenominator, LevelOf, XpOf, (id, level, xp) => _byId[id] = (level, xp));
        }
    }
}
