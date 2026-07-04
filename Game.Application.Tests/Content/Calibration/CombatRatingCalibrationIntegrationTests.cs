using Game.Abstractions.Content;
using Game.Abstractions.DataAccess;
using Game.Application.Content.Calibration;
using Game.Application.Services;
using Game.Core;
using Game.Core.Battle;
using Game.Core.Players;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CoreClass = Game.Core.Classes.Class;
using CoreEnemy = Game.Core.Enemies.Enemy;

namespace Game.Application.Tests.Content.Calibration
{
    /// <summary>
    /// The re-runnable calibration report (#1533, spike #1526 Decision 10) against the real seeded content:
    /// old-vs-new pricing for every authored enemy, where each combat zone's spawn table places on the
    /// <c>r = enemy/player</c> axis, and the <c>k</c>/proficiency-pie constants #1532 needs. This is a report a
    /// human reads during tuning (logged via <see cref="ITestOutputHelper"/>), not an assertion gate on
    /// specific numbers — the assertions here only pin the report's structural invariants (it runs clean and
    /// produces sane values) so a future content or engine change that breaks the tool itself fails loudly.
    /// <para>
    /// Current authored content is a minimal placeholder seed (one class, four skills, no items/mods/
    /// proficiencies, no DoT/reflect/buff-effect skills), so the reference builds below can only vary by core
    /// attribute allocation (offense vs. survivability) rather than the richer tank/reflect/DoT/buff-centric
    /// archetypes the spike anticipates — this report is generic over whatever content is loaded, and will pick
    /// up that variety automatically once it is authored (tracked as a follow-up).
    /// </para>
    /// </summary>
    [Collection("Integration")]
    public class CombatRatingCalibrationIntegrationTests : ApplicationIntegrationTestBase
    {
        private const int LevelSamplesPerZone = 3;
        private const int SeedsPerMatchup = 5;

        private readonly ITestOutputHelper _output;

        public CombatRatingCalibrationIntegrationTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper)
        {
            _output = testOutputHelper;
        }

        [Fact]
        public async Task CalibrationReport_AgainstSeededContent_ProducesASaneReport()
        {
            await SeedCommittedContentAsync();

            using var scope = CreateScope();
            var provider = scope.ServiceProvider;
            var zonesRepo = provider.GetRequiredService<IZones>();
            var enemiesRepo = provider.GetRequiredService<IEnemies>();
            var classesRepo = provider.GetRequiredService<IClasses>();
            var skillsRepo = provider.GetRequiredService<ISkills>();
            var itemsRepo = provider.GetRequiredService<IItems>();
            var itemModsRepo = provider.GetRequiredService<IItemMods>();
            var proficienciesRepo = provider.GetRequiredService<IProficiencies>();

            var zoneArcs = BuildZoneArcs(zonesRepo, enemiesRepo);
            var pricedEnemies = BuildPricedEnemyList(zonesRepo, enemiesRepo);
            var builds = BuildReferenceBuilds(classesRepo, skillsRepo, itemsRepo, itemModsRepo, proficienciesRepo);

            Assert.NotEmpty(zoneArcs);
            Assert.NotEmpty(pricedEnemies);
            Assert.NotEmpty(builds);

            var report = CombatRatingCalibrator.BuildReport(
                pricedEnemies, zoneArcs, builds, BattleService.PostBattleCooldown,
                LevelSamplesPerZone, SeedsPerMatchup, ServerGameConstants.ProficiencyXpPerVictory);

            LogReport(report);

            Assert.NotEmpty(report.EnemyPricing);
            Assert.NotEmpty(report.ZonePlacement);
            Assert.NotEmpty(report.RewardCurve);
            Assert.All(report.EnemyPricing, row => Assert.True(row.NewRating > 0));
            Assert.All(report.ZonePlacement, row => Assert.True(row.PlayerNewRating > 0 && row.SpawnTableNewRating > 0));
            Assert.True(report.RecommendedConstants.XpScaleK > 0);
            Assert.True(report.RecommendedConstants.ProficiencyPie > 0);
        }

        private async Task SeedCommittedContentAsync()
        {
            using var scope = CreateScope();
            var reader = scope.ServiceProvider.GetRequiredService<IContentImportReader>();
            var seeder = scope.ServiceProvider.GetRequiredService<IContentSeeder>();
            await seeder.SeedAsync(reader.Read(RepoPaths.ContentDirectory()), CancellationToken);
            await ReloadReferenceCachesAsync();
        }

        // One combat zone (Home excluded, and any zone left with no spawnable enemies) per authored zone, with
        // its idle-encounter spawn table wired to resolve a fresh, battle-ready Enemy per level.
        private static IReadOnlyList<ZoneArc> BuildZoneArcs(IZones zonesRepo, IEnemies enemiesRepo)
        {
            var enemiesById = enemiesRepo.All().ToDictionary(e => e.Id);

            return [.. zonesRepo.All()
                .Where(zone => zone.RetiredAt is null && !zonesRepo.IsHomeZone(zone.Id) && enemiesRepo.HasSpawnableEnemies(zone.Id))
                .Select(zone =>
                {
                    var spawns = enemiesById.Values
                        .SelectMany(enemy => enemy.Spawns.Where(spawn => spawn.ZoneId == zone.Id)
                            .Select(spawn => new ZoneSpawn(
                                enemy.Id, enemy.Name, spawn.Weight,
                                level => ResolveBattleReadyEnemy(enemiesRepo, enemy.Id, level))))
                        .ToList();

                    return new ZoneArc(zone.Id, zone.Name, zone.LevelMin, zone.LevelMax, spawns);
                })];
        }

        // One representative (enemy, level) pair per authored enemy — the level of the first combat zone it's
        // found in (its spawn table, or as the zone's dedicated boss), at that zone's arc midpoint (or its
        // fixed boss level). An enemy authored but not placed in any live zone is skipped — it isn't part of
        // the live pricing picture the report is checking.
        private static IReadOnlyList<(int Id, string Name, int Level, CoreEnemy Enemy)> BuildPricedEnemyList(
            IZones zonesRepo, IEnemies enemiesRepo)
        {
            var liveZones = zonesRepo.All().Where(zone => zone.RetiredAt is null).ToList();
            var priced = new Dictionary<int, (int Id, string Name, int Level, CoreEnemy Enemy)>();

            foreach (var zone in liveZones)
            {
                var midLevel = (zone.LevelMin + zone.LevelMax) / 2;
                foreach (var enemy in enemiesRepo.All().Where(e => e.RetiredAt is null))
                {
                    if (priced.ContainsKey(enemy.Id))
                    {
                        continue;
                    }

                    if (enemy.Spawns.Any(spawn => spawn.ZoneId == zone.Id))
                    {
                        priced[enemy.Id] = (enemy.Id, enemy.Name, midLevel, ResolveBattleReadyEnemy(enemiesRepo, enemy.Id, midLevel));
                    }
                    else if (zone.BossEnemyId == enemy.Id)
                    {
                        priced[enemy.Id] = (enemy.Id, enemy.Name, zone.BossLevel, ResolveBattleReadyEnemy(enemiesRepo, enemy.Id, zone.BossLevel));
                    }
                }
            }

            return [.. priced.Values];
        }

        // Resolves the domain enemy and selects its full authored loadout (rather than the random per-encounter
        // draw) so the report is deterministic and reproducible across runs against unchanged content.
        private static CoreEnemy ResolveBattleReadyEnemy(IEnemies enemiesRepo, int enemyId, int level)
        {
            var enemy = enemiesRepo.GetDomainEnemy(enemyId, level)
                ?? throw new InvalidOperationException($"Enemy {enemyId} does not resolve at level {level}.");
            enemy.SelectAllBattleSkills();
            return enemy;
        }

        // Three archetypes differentiated by free-pool attribute allocation — the only build-diversity today's
        // minimal seed content supports (no items, mods, or proficiencies are authored, and no skill carries a
        // DoT/reflect/buff effect yet). Each is assembled through the real BattleSnapshot/battler path against
        // the single authored class, so the rating sees exactly what a battle would.
        private static IReadOnlyList<ReferenceBuild> BuildReferenceBuilds(
            IClasses classesRepo, ISkills skillsRepo, IItems itemsRepo, IItemMods itemModsRepo, IProficiencies proficienciesRepo)
        {
            var playerClass = classesRepo.All().Single(c => c.RetiredAt is null);
            var skillIds = playerClass.StarterSkillIds.Take(GameConstants.MaxSelectedSkills).ToList();

            CoreClass ResolveClass(int id) => classesRepo.GetClass(id)
                ?? throw new InvalidOperationException($"Class {id} does not resolve.");

            ReferenceBuild MakeArchetype(string name, IReadOnlyDictionary<EAttribute, double> weights)
            {
                BattleSnapshot MakeSnapshot(int level) => new()
                {
                    Level = level,
                    ClassId = playerClass.Id,
                    StatAllocations = [.. weights.Select(w => new StatAllocation
                    {
                        Attribute = w.Key,
                        Amount = level * GameConstants.StatPointsPerLevel * w.Value,
                    })],
                    EquippedItems = [],
                    SkillIds = skillIds,
                };

                Battler BuildBattler(int level) => MakeSnapshot(level).ToBattler(
                    itemsRepo.GetItem, itemModsRepo.GetItemMod, skillsRepo.TryGetSkill,
                    proficienciesRepo.GetProficiency, ResolveClass);

                double OldMeasure(int level) => DefeatRewards.SumCoreAttributes(
                    MakeSnapshot(level).GetModifiersWithSignaturePassive(
                        itemsRepo.GetItem, itemModsRepo.GetItemMod, proficienciesRepo.GetProficiency, ResolveClass));

                return new ReferenceBuild(name, BuildBattler, OldMeasure);
            }

            return
            [
                MakeArchetype("Offense (STR)", new Dictionary<EAttribute, double> { [EAttribute.Strength] = 1.0 }),
                MakeArchetype("Survivability (END)", new Dictionary<EAttribute, double> { [EAttribute.Endurance] = 1.0 }),
                MakeArchetype("Balanced (STR/END)", new Dictionary<EAttribute, double> { [EAttribute.Strength] = 0.5, [EAttribute.Endurance] = 0.5 }),
            ];
        }

        private void LogReport(CalibrationReport report)
        {
            _output.WriteLine("=== Enemy pricing: old (SumCoreAttributes) vs new (CombatRating) ===");
            foreach (var row in report.EnemyPricing.OrderBy(r => r.EnemyId))
            {
                _output.WriteLine(
                    $"  [{row.EnemyId,2}] {row.EnemyName,-20} L{row.Level,-3} old={row.OldMeasure,8:F1} new={row.NewRating,8:F2} "
                    + $"share {row.OldShare:P1} -> {row.NewShare:P1} (shift x{row.RelativeShift:F2})");
            }

            _output.WriteLine(string.Empty);
            _output.WriteLine("=== Zone placement: r = enemy/player, old vs new ===");
            foreach (var row in report.ZonePlacement.OrderBy(r => r.ZoneId).ThenBy(r => r.Level).ThenBy(r => r.BuildName))
            {
                _output.WriteLine(
                    $"  {row.ZoneName,-16} L{row.Level,-3} {row.BuildName,-22} oldR={row.OldRatio,6:F2} newR={row.NewRatio,6:F2}");
            }

            _output.WriteLine(string.Empty);
            var anchor = report.RecommendedConstants.Anchor;
            _output.WriteLine("=== Recommended constants (spike #1526 Decision 10, for #1532) ===");
            _output.WriteLine($"  Anchor: {anchor.ZoneName} L{anchor.Level}, {anchor.BuildName}");
            _output.WriteLine($"  k (XP scale)      = {report.RecommendedConstants.XpScaleK:F4}");
            _output.WriteLine($"  proficiency pie   = {report.RecommendedConstants.ProficiencyPie:F4}");

            _output.WriteLine(string.Empty);
            _output.WriteLine("=== Reward curve under recommended k ===");
            foreach (var point in report.RewardCurve.OrderBy(p => p.ZoneId).ThenBy(p => p.Level).ThenBy(p => p.BuildName))
            {
                _output.WriteLine(
                    $"  {point.ZoneName,-16} L{point.Level,-3} {point.BuildName,-22} r={point.Ratio,5:F2} "
                    + $"xp/kill={point.XpPerKill,7:F2} winRate={point.WinRate,5:P0} avgSec={point.AvgBattleSeconds,6:F1} "
                    + $"xp/hr={point.XpPerHour,8:F1}");
            }
        }
    }
}
