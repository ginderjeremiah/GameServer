using Game.Core.Battle;
using Game.Core.Enemies;

namespace Game.Application.Content.Calibration
{
    /// <summary>
    /// A named player archetype sampled at a given level (spike #1526, calibration report #1533). Built fresh
    /// on every call — a <see cref="Battler"/> is stateful (battle simulation mutates health/effects), so a
    /// single instance cannot be reused across a rating read and a simulated battle, or across two battles.
    /// </summary>
    public sealed record ReferenceBuild(string Name, Func<int, Battler> BuildBattler, Func<int, double> OldMeasure);

    /// <summary>One entry of a zone's spawn table, resolved to a fresh <see cref="Enemy"/> for a given level.</summary>
    public sealed record ZoneSpawn(int EnemyId, string EnemyName, int Weight, Func<int, Enemy> Resolve);

    /// <summary>A combat zone's idle-encounter level range and spawn table, as read from the live reference data.</summary>
    public sealed record ZoneArc(int ZoneId, string Name, int LevelMin, int LevelMax, IReadOnlyList<ZoneSpawn> Spawns);

    /// <summary>
    /// Old-vs-new pricing for one authored enemy at a representative level. <see cref="RelativeShift"/> compares
    /// each side's <em>share</em> of the total pricing across the population (rather than the raw values, which
    /// live on different, incomparable scales) — a shift far from 1 means the swap re-prices this enemy relative
    /// to its peers, which is what a content author needs flagged (spike #1526; feeds the #1529 lint).
    /// </summary>
    public sealed record EnemyPricingRow(int EnemyId, string EnemyName, int Level, double OldMeasure, double NewRating, double OldShare, double NewShare)
    {
        public double RelativeShift => NewShare / OldShare;
    }

    /// <summary>
    /// Where a reference build's rating places one zone's (weighted) spawn table on the <c>r = enemy/player</c>
    /// axis, computed under both the outgoing <c>SumCoreAttributes</c> measure and the incoming
    /// <see cref="CombatRating"/> — so a content author can see directly whether the swap moves a zone off its
    /// intended difficulty band (spike #1526 Decision 10, content-design.md's per-level-band placement).
    /// </summary>
    public sealed record ZonePlacementRow(
        int ZoneId, string ZoneName, int Level, string BuildName,
        double PlayerOldMeasure, double SpawnTableOldMeasure, double OldRatio,
        double PlayerNewRating, double SpawnTableNewRating, double NewRatio);

    /// <summary>The zone/level/build sample used to anchor the recommended constants — the "mid-arc reference
    /// point" spike #1526 Decision 10 calls for, chosen as whichever sample the <em>current</em> game already
    /// treats as matched (its old-measure ratio closest to 1).</summary>
    public sealed record CalibrationAnchor(
        int ZoneId, string ZoneName, int Level, string BuildName,
        double PlayerOldMeasure, double EnemyOldMeasure, double PlayerNewRating, double EnemyNewRating);

    /// <summary>
    /// The recommended <c>k</c> (XP scale) and proficiency <c>pie</c> for the #1532 consumer swap: the pair of
    /// constants that make the new bounty curve and max-normalized accrual pay the same as today's curve does
    /// at <see cref="Anchor"/>, so the swap is continuous at a mid-arc reference point rather than an arbitrary
    /// rescale (spike #1526 Decision 10).
    /// </summary>
    public sealed record RecommendedConstants(double XpScaleK, double ProficiencyPie, CalibrationAnchor Anchor);

    /// <summary>
    /// One (zone, level, build) sample's XP/hour under the proposed bounty curve (<c>k × enemyRating ×
    /// min(r,1)²</c>), with the win rate and average battle duration measured by actually running
    /// <see cref="BattleSimulator"/> — the ground truth for "time per kill" rather than a formula-derived
    /// approximation (spike #1526 Decision 10's per-hour curve check).
    /// </summary>
    public sealed record RewardCurvePoint(
        int ZoneId, string ZoneName, int Level, string BuildName,
        double PlayerRating, double SpawnTableRating, double Ratio,
        double XpPerKill, double WinRate, double AvgBattleSeconds, double XpPerHour);

    /// <summary>The full calibration report (#1533): old-vs-new enemy pricing, zone placement under both
    /// measures, the recommended <c>k</c>/pie constants, and the resulting XP/hour curve across the zone arc.
    /// A human reads this during tuning; it is not wired into any runtime system (that is #1532's job).</summary>
    public sealed record CalibrationReport(
        IReadOnlyList<EnemyPricingRow> EnemyPricing,
        IReadOnlyList<ZonePlacementRow> ZonePlacement,
        RecommendedConstants RecommendedConstants,
        IReadOnlyList<RewardCurvePoint> RewardCurve);
}
