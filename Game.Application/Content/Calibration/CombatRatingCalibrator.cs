using Game.Core;
using Game.Core.Battle;
using Game.Core.Enemies;

namespace Game.Application.Content.Calibration
{
    /// <summary>
    /// The combat-rating calibration report (#1533, spike #1526 Decision 10): compares the outgoing
    /// <c>DefeatRewards.SumCoreAttributes</c> power measure against the incoming <see cref="CombatRating"/>
    /// across authored enemies and zones, and recommends the <c>k</c>/proficiency-pie constants the #1532
    /// consumer swap needs. Pure over already-resolved domain objects — the caller (an integration test,
    /// since building real reference builds needs the seeded catalog) supplies the enemies/zones/reference
    /// builds; this class holds none of the data-access wiring.
    /// <para>
    /// Read-only, no persistence: this is tooling a human runs and reads during tuning, kept runnable so it can
    /// be re-run after content additions or reference-constant changes to spot pricing drift (it does not
    /// itself change <see cref="ServerGameConstants"/> or any authored content).
    /// </para>
    /// </summary>
    public static class CombatRatingCalibrator
    {
        /// <summary>
        /// Builds the full report: enemy pricing, zone placement, the recommended constants (anchored off the
        /// zone-placement samples), and the resulting reward curve under the recommended <c>k</c>.
        /// </summary>
        public static CalibrationReport BuildReport(
            IReadOnlyList<(int Id, string Name, int Level, Enemy Enemy)> enemies,
            IReadOnlyList<ZoneArc> zones,
            IReadOnlyList<ReferenceBuild> builds,
            TimeSpan postBattleCooldown,
            int levelSamplesPerZone = 3,
            int seedsPerMatchup = 5,
            double currentProficiencyPie = ServerGameConstants.ProficiencyXpPerVictory)
        {
            var enemyPricing = PriceEnemies(enemies);
            var zonePlacement = PlaceZones(zones, builds, levelSamplesPerZone);
            var recommended = RecommendConstants(zonePlacement, currentProficiencyPie);
            var rewardCurve = ComputeRewardCurve(
                zones, builds, recommended.XpScaleK, postBattleCooldown, levelSamplesPerZone, seedsPerMatchup);

            return new CalibrationReport(enemyPricing, zonePlacement, recommended, rewardCurve);
        }

        /// <summary>
        /// Old-vs-new pricing for each enemy, each at the level the caller resolved it at (typically a
        /// representative point in the zone(s) it spawns in). <see cref="EnemyPricingRow.RelativeShift"/>
        /// compares each enemy's share of the total pricing before and after the swap.
        /// </summary>
        public static IReadOnlyList<EnemyPricingRow> PriceEnemies(
            IReadOnlyList<(int Id, string Name, int Level, Enemy Enemy)> enemies)
        {
            if (enemies.Count == 0)
            {
                return [];
            }

            var priced = enemies
                .Select(e => (
                    e.Id,
                    e.Name,
                    e.Level,
                    Old: DefeatRewards.SumCoreAttributes(e.Enemy.GetAttributeModifiers()),
                    New: CombatRating.Rate(e.Enemy.ToBattler(), isPlayer: false)))
                .ToList();

            var oldTotal = priced.Sum(p => p.Old);
            var newTotal = priced.Sum(p => p.New);

            return [.. priced.Select(p => new EnemyPricingRow(
                p.Id, p.Name, p.Level, p.Old, p.New, p.Old / oldTotal, p.New / newTotal))];
        }

        /// <summary>
        /// For every zone, at <paramref name="levelSamplesPerZone"/> evenly-spaced levels across its idle
        /// encounter range, rates each reference build against the zone's spawn-weighted enemy rating — under
        /// both the old measure and the new rating, so the two placements are directly comparable.
        /// </summary>
        public static IReadOnlyList<ZonePlacementRow> PlaceZones(
            IReadOnlyList<ZoneArc> zones, IReadOnlyList<ReferenceBuild> builds, int levelSamplesPerZone = 3)
        {
            var rows = new List<ZonePlacementRow>();
            foreach (var zone in zones)
            {
                foreach (var level in SampleLevels(zone.LevelMin, zone.LevelMax, levelSamplesPerZone))
                {
                    var (spawnOld, spawnNew) = WeightedSpawnMeasures(zone, level);
                    foreach (var build in builds)
                    {
                        var playerOld = build.OldMeasure(level);
                        var playerNew = CombatRating.Rate(build.BuildBattler(level), isPlayer: true);

                        rows.Add(new ZonePlacementRow(
                            zone.ZoneId, zone.Name, level, build.Name,
                            playerOld, spawnOld, spawnOld / playerOld,
                            playerNew, spawnNew, spawnNew / playerNew));
                    }
                }
            }

            return rows;
        }

        /// <summary>
        /// Recommends <c>k</c> and the proficiency pie by anchoring on the zone-placement sample the
        /// <em>current</em> game already treats as matched (its old-measure ratio closest to 1 in log-space, so
        /// an equally-off-in-either-direction pair of candidates ties fairly): <c>k</c> is solved so the new
        /// bounty curve pays the same XP the old band-clamped curve would at that anchor, and the pie is
        /// rescaled by the ratio of the new-to-old player measure there — the "keep payouts roughly continuous
        /// with today for a mid-arc reference point" goal from spike #1526 Decision 10.
        /// </summary>
        public static RecommendedConstants RecommendConstants(
            IReadOnlyList<ZonePlacementRow> zonePlacement,
            double currentProficiencyPie = ServerGameConstants.ProficiencyXpPerVictory)
        {
            if (zonePlacement.Count == 0)
            {
                throw new ArgumentException("Cannot recommend constants with no zone-placement samples.", nameof(zonePlacement));
            }

            var anchorRow = zonePlacement.MinBy(row => Math.Abs(Math.Log(row.OldRatio)))!;
            if (anchorRow.PlayerOldMeasure <= 0)
            {
                throw new InvalidOperationException(
                    $"The calibration anchor ({anchorRow.ZoneName} L{anchorRow.Level}, {anchorRow.BuildName}) has a "
                    + "non-positive old power measure — a degenerate reference build, not a value to divide by.");
            }

            // The old curve pays exactly the enemy's old measure at a matched ratio (DifficultyMultiplier == 1),
            // so that is the "today" XP the new curve is anchored to reproduce at this sample.
            var xpUnderOldCurveAtAnchor = anchorRow.SpawnTableOldMeasure;
            var clampedNewRatio = Math.Min(anchorRow.NewRatio, 1.0);
            var xpScaleK = xpUnderOldCurveAtAnchor / (anchorRow.SpawnTableNewRating * clampedNewRatio * clampedNewRatio);
            var proficiencyPie = currentProficiencyPie * (anchorRow.PlayerNewRating / anchorRow.PlayerOldMeasure);

            var anchor = new CalibrationAnchor(
                anchorRow.ZoneId, anchorRow.ZoneName, anchorRow.Level, anchorRow.BuildName,
                anchorRow.PlayerOldMeasure, anchorRow.SpawnTableOldMeasure,
                anchorRow.PlayerNewRating, anchorRow.SpawnTableNewRating);

            return new RecommendedConstants(xpScaleK, proficiencyPie, anchor);
        }

        /// <summary>
        /// The XP/hour curve across the zone arc under the proposed bounty curve, using real
        /// <see cref="BattleSimulator"/> runs (not a formula-derived approximation) for win rate and average
        /// battle duration — the direct check that both build-optimization and zone advancement pay per hour
        /// (spike #1526 Decision 4/10). Seeds are deterministic (hashed from the matchup), so the report is
        /// reproducible across runs against unchanged content. <paramref name="postBattleCooldown"/> is the
        /// real inter-battle gap (the caller passes the live <c>BattleService.PostBattleCooldown</c>) so this
        /// tool has no hand-duplicated copy of that constant.
        /// </summary>
        public static IReadOnlyList<RewardCurvePoint> ComputeRewardCurve(
            IReadOnlyList<ZoneArc> zones, IReadOnlyList<ReferenceBuild> builds, double xpScaleK,
            TimeSpan postBattleCooldown, int levelSamplesPerZone = 3, int seedsPerMatchup = 5)
        {
            var cooldownSeconds = postBattleCooldown.TotalSeconds;
            var points = new List<RewardCurvePoint>();

            foreach (var zone in zones)
            {
                foreach (var level in SampleLevels(zone.LevelMin, zone.LevelMax, levelSamplesPerZone))
                {
                    var (_, spawnNew) = WeightedSpawnMeasures(zone, level);
                    foreach (var build in builds)
                    {
                        var playerRating = CombatRating.Rate(build.BuildBattler(level), isPlayer: true);
                        var ratio = spawnNew / playerRating;
                        var xpPerKill = xpScaleK * spawnNew * Math.Pow(Math.Min(ratio, 1.0), 2);

                        var (winRate, avgSeconds) = SimulateMatchups(zone, level, build, seedsPerMatchup);
                        var perBattleSeconds = avgSeconds + cooldownSeconds;
                        var xpPerHour = perBattleSeconds > 0 ? winRate * xpPerKill / perBattleSeconds * 3600.0 : 0.0;

                        points.Add(new RewardCurvePoint(
                            zone.ZoneId, zone.Name, level, build.Name,
                            playerRating, spawnNew, ratio, xpPerKill, winRate, avgSeconds, xpPerHour));
                    }
                }
            }

            return points;
        }

        // The zone's spawn-weighted average old measure and new rating at a level — the "typical enemy" this
        // zone presents at that level, for placement/pricing against a reference build.
        private static (double Old, double New) WeightedSpawnMeasures(ZoneArc zone, int level)
        {
            if (zone.Spawns.Count == 0)
            {
                throw new ArgumentException(
                    $"Zone {zone.Name} ({zone.ZoneId}) has no spawn-table entries — every combat zone must have "
                    + "at least one spawnable enemy.", nameof(zone));
            }

            var totalWeight = 0.0;
            var weightedOld = 0.0;
            var weightedNew = 0.0;
            foreach (var spawn in zone.Spawns)
            {
                var enemy = spawn.Resolve(level);
                weightedOld += spawn.Weight * DefeatRewards.SumCoreAttributes(enemy.GetAttributeModifiers());
                weightedNew += spawn.Weight * CombatRating.Rate(enemy.ToBattler(), isPlayer: false);
                totalWeight += spawn.Weight;
            }

            return (weightedOld / totalWeight, weightedNew / totalWeight);
        }

        // Runs seedsPerMatchup deterministic battles against every spawn-table entry (weighted by spawn
        // weight), returning the spawn-weighted win rate and average battle duration across all of them —
        // the ground-truth "time per battle slot" the XP/hour figure divides by.
        private static (double WinRate, double AvgSeconds) SimulateMatchups(
            ZoneArc zone, int level, ReferenceBuild build, int seedsPerMatchup)
        {
            double weightedWins = 0;
            double weightedMs = 0;
            double weightUnits = 0;

            foreach (var spawn in zone.Spawns)
            {
                for (var i = 0; i < seedsPerMatchup; i++)
                {
                    var seed = unchecked((uint)HashCode.Combine(zone.ZoneId, level, build.Name, spawn.EnemyId, i));
                    var result = new BattleSimulator(build.BuildBattler(level), spawn.Resolve(level).ToBattler(), seed).Simulate();

                    weightedWins += spawn.Weight * (result.Victory ? 1 : 0);
                    weightedMs += spawn.Weight * result.TotalMs;
                    weightUnits += spawn.Weight;
                }
            }

            return weightUnits > 0 ? (weightedWins / weightUnits, weightedMs / weightUnits / 1000.0) : (0.0, 0.0);
        }

        // Evenly-spaced, deduplicated levels across [min, max] — samples ≤ 1 or a single-level range collapse
        // to just the minimum.
        private static IReadOnlyList<int> SampleLevels(int min, int max, int samples)
        {
            if (samples <= 1 || min >= max)
            {
                return [min];
            }

            return [.. Enumerable.Range(0, samples)
                .Select(i => min + (int)Math.Round((max - min) * (double)i / (samples - 1)))
                .Distinct()];
        }
    }
}
