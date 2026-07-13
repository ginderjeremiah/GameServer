using Game.Application.Content.Calibration;
using Game.Core;
using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Skills;
using Xunit;
using static Game.Core.EAttribute;

namespace Game.Application.Tests.Content.Calibration
{
    /// <summary>
    /// Pure unit coverage for <see cref="CombatRatingCalibrator"/> (#1533, spike #1526 Decision 10) — the math
    /// that turns already-resolved domain objects into the calibration report. No database: every enemy, zone,
    /// and reference build is hand-built, mirroring <c>CombatRatingTests</c>' fixture style. DB-backed coverage
    /// against the real seeded content lives in <c>CombatRatingCalibrationIntegrationTests</c>.
    /// </summary>
    public class CombatRatingCalibratorTests
    {
        private static readonly TimeSpan TestCooldown = TimeSpan.FromSeconds(5);

        // ── PriceEnemies ─────────────────────────────────────────────────────

        [Fact]
        public void PriceEnemies_NoEnemies_ReturnsEmpty()
        {
            Assert.Empty(CombatRatingCalibrator.PriceEnemies([]));
        }

        [Fact]
        public void PriceEnemies_SumsSharesToOneAcrossThePopulation()
        {
            var enemies = new[]
            {
                (Id: 0, Name: "Weak", Level: 1, Enemy: MakeEnemy(strength: 5, endurance: 5)),
                (Id: 1, Name: "Strong", Level: 1, Enemy: MakeEnemy(strength: 50, endurance: 50)),
            };

            var rows = CombatRatingCalibrator.PriceEnemies(enemies);

            Assert.Equal(1.0, rows.Sum(r => r.OldShare), 6);
            Assert.Equal(1.0, rows.Sum(r => r.NewShare), 6);
        }

        [Fact]
        public void PriceEnemies_EnemyWithDeadAttributeAllocation_RelativeShiftIsBelowOne()
        {
            // A LUK-heavy enemy (asymmetry-gated: enemies never crit/parry) inflates the old sum-of-cores
            // measure without adding any threat — the exact defect #1526 fixes. Its new rating share should
            // shrink relative to its old share once the dead LUK stops counting.
            var deadStatEnemy = (Id: 0, Name: "LuckHeavy", Level: 1, Enemy: MakeEnemy(strength: 10, endurance: 10, luck: 40));
            var plainEnemy = (Id: 1, Name: "Plain", Level: 1, Enemy: MakeEnemy(strength: 10, endurance: 10));

            var rows = CombatRatingCalibrator.PriceEnemies([deadStatEnemy, plainEnemy]);

            var luckHeavyRow = rows.Single(r => r.EnemyId == 0);
            Assert.True(luckHeavyRow.RelativeShift < 1.0);
        }

        // ── PlaceZones ───────────────────────────────────────────────────────

        [Fact]
        public void PlaceZones_SamplesRequestedLevelsAndEveryBuild()
        {
            var zone = MakeZone(zoneId: 0, levelMin: 1, levelMax: 10, MakeSpawn(1, weight: 1));
            var builds = new[] { MakeBuild("Build A"), MakeBuild("Build B") };

            var rows = CombatRatingCalibrator.PlaceZones([zone], builds, levelSamplesPerZone: 3);

            Assert.Equal(6, rows.Count); // 3 levels × 2 builds
            Assert.All(rows, r => Assert.Contains(r.Level, new[] { 1, 5, 10 }));
            Assert.Contains(rows, r => r.BuildName == "Build A");
            Assert.Contains(rows, r => r.BuildName == "Build B");
        }

        [Fact]
        public void PlaceZones_NoSpawnsInZone_ThrowsRatherThanDivideByZero()
        {
            var emptyZone = MakeZone(zoneId: 0, levelMin: 1, levelMax: 5);

            Assert.Throws<ArgumentException>(() => CombatRatingCalibrator.PlaceZones([emptyZone], [MakeBuild("Build")]));
        }

        [Fact]
        public void PlaceZones_WeightsSpawnsByTheirSpawnWeight()
        {
            // A 3x-weighted weak enemy alongside an unweighted strong one should pull the zone's placement
            // toward the weak enemy rather than a plain average of the two.
            var weak = MakeSpawn(enemyId: 0, weight: 3, strength: 5, endurance: 5);
            var strong = MakeSpawn(enemyId: 1, weight: 1, strength: 50, endurance: 50);
            var evenlyWeighted = MakeZone(zoneId: 0, levelMin: 1, levelMax: 1, weak with { Weight = 1 }, strong);
            var heavilyWeightedTowardWeak = MakeZone(zoneId: 1, levelMin: 1, levelMax: 1, weak, strong);

            var build = MakeBuild("Build");
            var evenRows = CombatRatingCalibrator.PlaceZones([evenlyWeighted], [build], levelSamplesPerZone: 1);
            var weightedRows = CombatRatingCalibrator.PlaceZones([heavilyWeightedTowardWeak], [build], levelSamplesPerZone: 1);

            Assert.True(weightedRows[0].SpawnTableNewRating < evenRows[0].SpawnTableNewRating);
        }

        // ── RecommendConstants ───────────────────────────────────────────────

        [Fact]
        public void RecommendConstants_NoSamples_Throws()
        {
            Assert.Throws<ArgumentException>(() => CombatRatingCalibrator.RecommendConstants([]));
        }

        [Fact]
        public void RecommendConstants_AnchorsOnTheSampleClosestToAMatchedOldRatio()
        {
            var farUnder = MakeZonePlacementRow(level: 1, oldRatio: 0.1, newRatio: 0.1);
            var matched = MakeZonePlacementRow(level: 5, oldRatio: 1.02, newRatio: 1.1);
            var farOver = MakeZonePlacementRow(level: 10, oldRatio: 5.0, newRatio: 5.0);

            var recommended = CombatRatingCalibrator.RecommendConstants([farUnder, matched, farOver]);

            Assert.Equal(5, recommended.Anchor.Level);
        }

        [Fact]
        public void RecommendConstants_XpScaleK_ReproducesTodaysXpAtTheAnchor()
        {
            var matched = MakeZonePlacementRow(
                level: 5, oldRatio: 1.0, newRatio: 1.25,
                playerOld: 100, spawnOld: 100, playerNew: 40, spawnNew: 50);

            var recommended = CombatRatingCalibrator.RecommendConstants([matched]);

            // xpUnderOldCurve = spawnOld (matched ⇒ multiplier 1) = 100; new formula at ratio 1.25 clamps to 1,
            // so k × 50 × 1² must equal 100 ⇒ k = 2.
            Assert.Equal(2.0, recommended.XpScaleK, 6);
        }

        [Fact]
        public void RecommendConstants_AnchorOutsideTheMatchedBand_FoldsInTheOldDifficultyMultiplier()
        {
            // oldRatio 0.5 sits below the ±20% band, so the outgoing curve's real payout here is
            // spawnOld × ratio² = 50 × 0.25 = 12.5 — not the bare 50 an assumed-matched anchor would use.
            var belowBand = MakeZonePlacementRow(
                level: 5, oldRatio: 0.5, newRatio: 0.6,
                playerOld: 100, spawnOld: 50, playerNew: 50, spawnNew: 30);

            var recommended = CombatRatingCalibrator.RecommendConstants([belowBand]);

            var expectedOldXp = 50 * DefeatRewards.GetDifficultyMultiplier(50, 100);
            var expectedK = expectedOldXp / (30 * Math.Pow(Math.Min(0.6, 1.0), 2));
            Assert.Equal(expectedK, recommended.XpScaleK, 6);
        }

        [Fact]
        public void RecommendConstants_ProficiencyPie_RescalesByThePlayerMeasureRatio()
        {
            var matched = MakeZonePlacementRow(
                level: 5, oldRatio: 1.0, newRatio: 1.0,
                playerOld: 100, spawnOld: 100, playerNew: 50, spawnNew: 50);

            var recommended = CombatRatingCalibrator.RecommendConstants([matched], currentProficiencyPie: 10.0);

            // playerNew ÷ playerOld = 0.5 ⇒ pie halves so the same in-battle activity claims the same share.
            Assert.Equal(5.0, recommended.ProficiencyPie, 6);
        }

        // ── ComputeRewardCurve ───────────────────────────────────────────────

        [Fact]
        public void ComputeRewardCurve_MatchedFight_HasPositiveXpPerHour()
        {
            var zone = MakeZone(zoneId: 0, levelMin: 5, levelMax: 5, MakeSpawn(0, weight: 1, strength: 15, endurance: 15));
            var build = MakeBuild("Build", strength: 15, endurance: 15);

            var points = CombatRatingCalibrator.ComputeRewardCurve(
                [zone], [build], xpScaleK: 1.0, TestCooldown, levelSamplesPerZone: 1, seedsPerMatchup: 3);

            Assert.Single(points);
            Assert.True(points[0].XpPerHour > 0);
            Assert.True(points[0].WinRate is > 0 and <= 1.0);
        }

        [Fact]
        public void ComputeRewardCurve_IsDeterministicAcrossRuns()
        {
            var zone = MakeZone(zoneId: 0, levelMin: 5, levelMax: 5, MakeSpawn(0, weight: 1, strength: 15, endurance: 15));
            var build = MakeBuild("Build", strength: 15, endurance: 15);

            var first = CombatRatingCalibrator.ComputeRewardCurve(
                [zone], [build], xpScaleK: 1.0, TestCooldown, levelSamplesPerZone: 1, seedsPerMatchup: 3);
            var second = CombatRatingCalibrator.ComputeRewardCurve(
                [zone], [build], xpScaleK: 1.0, TestCooldown, levelSamplesPerZone: 1, seedsPerMatchup: 3);

            Assert.Equal(first[0].WinRate, second[0].WinRate, 6);
            Assert.Equal(first[0].AvgBattleSeconds, second[0].AvgBattleSeconds, 6);
        }

        // ── MatchupSeed ──────────────────────────────────────────────────────

        [Fact]
        public void MatchupSeed_IsPinnedAcrossProcesses()
        {
            // In-process determinism alone doesn't catch the bug this guards against: HashCode.Combine is also
            // stable within a single process (it reseeds once at process start), so a same-process comparison
            // would have passed even with the old, non-reproducible implementation. Pinning the literal value
            // is the only way a unit test can assert stability *across* processes/runs.
            Assert.Equal(3800752286u, CombatRatingCalibrator.MatchupSeed(3, 5, "Build A", 7, 2));
        }

        [Fact]
        public void MatchupSeed_DiffersForDifferentBuildNames()
        {
            // .NET's built-in string hashing (used by HashCode.Combine) is randomized per process; MatchupSeed
            // must not derive from it. Distinct matchup keys should (in practice) yield distinct seeds.
            var seedA = CombatRatingCalibrator.MatchupSeed(0, 1, "Build A", 0, 0);
            var seedB = CombatRatingCalibrator.MatchupSeed(0, 1, "Build B", 0, 0);

            Assert.NotEqual(seedA, seedB);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static ZonePlacementRow MakeZonePlacementRow(
            int level, double oldRatio, double newRatio,
            double playerOld = 100, double spawnOld = 0, double playerNew = 50, double spawnNew = 0)
        {
            return new ZonePlacementRow(
                ZoneId: 0, ZoneName: "Zone", Level: level, BuildName: "Build",
                PlayerOldMeasure: playerOld, SpawnTableOldMeasure: spawnOld == 0 ? playerOld * oldRatio : spawnOld, OldRatio: oldRatio,
                PlayerNewRating: playerNew, SpawnTableNewRating: spawnNew == 0 ? playerNew * newRatio : spawnNew, NewRatio: newRatio);
        }

        private static ZoneArc MakeZone(int zoneId, int levelMin, int levelMax, params ZoneSpawn[] spawns)
        {
            return new ZoneArc(zoneId, $"Zone {zoneId}", levelMin, levelMax, spawns);
        }

        private static ZoneSpawn MakeSpawn(int enemyId, int weight, double strength = 10, double endurance = 10)
        {
            return new ZoneSpawn(enemyId, $"Enemy {enemyId}", weight, level => MakeEnemy(strength, endurance, level: level));
        }

        private static Enemy MakeEnemy(double strength = 10, double endurance = 10, double luck = 0, int level = 1)
        {
            var distributions = new List<AttributeDistribution>
            {
                new() { AttributeId = Strength, BaseAmount = (decimal)strength, AmountPerLevel = 0 },
                new() { AttributeId = Endurance, BaseAmount = (decimal)endurance, AmountPerLevel = 0 },
            };
            if (luck != 0)
            {
                distributions.Add(new AttributeDistribution { AttributeId = Luck, BaseAmount = (decimal)luck, AmountPerLevel = 0 });
            }

            var enemy = new Enemy
            {
                Id = 0,
                Name = "Test Enemy",
                Level = level,
                IsBoss = false,
                AttributeDistributions = distributions,
                AvailableSkills = [MakeSkill()],
            };
            enemy.SelectAllBattleSkills();
            return enemy;
        }

        private static ReferenceBuild MakeBuild(string name, double strength = 10, double endurance = 10)
        {
            Battler BuildBattler(int level)
            {
                var modifiers = new List<AttributeModifier>
                {
                    new() { Attribute = Strength, Amount = strength, Type = EModifierType.Additive, Source = EAttributeModifierSource.PlayerStatPoints },
                    new() { Attribute = Endurance, Amount = endurance, Type = EModifierType.Additive, Source = EAttributeModifierSource.PlayerStatPoints },
                };
                return new Battler(new AttributeCollection(modifiers), [MakeSkill()], level);
            }

            double OldMeasure(int level) => strength + endurance;

            return new ReferenceBuild(name, BuildBattler, OldMeasure);
        }

        private static Skill MakeSkill() => new()
        {
            Id = 1,
            Name = "Strike",
            Description = string.Empty,
            DamagePortions = [new SkillDamagePortion { Type = EDamageType.Physical, Weight = 1.0 }],
            CooldownMs = 1000,
            BaseDamage = 10,
            CriticalChance = 0,
            DamageMultipliers = [new DamageMultiplier { Attribute = Strength, Amount = 1.0 }],
            Effects = [],
        };
    }
}
