using Game.Abstractions.DataAccess;
using Game.Application.Services;
using Game.Core;
using Game.Core.Battle;
using Game.Core.Players;
using Game.Core.Proficiencies;
using Game.Core.Skills;
using Game.Core.TestInfrastructure.Builders;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.Services
{
    /// <summary>
    /// Spike #1343 part D — the end-to-end cross-school payoff. A multi-typed skill's direct hit is split into
    /// weighted portions by the (unmodified) battle pipeline (#1385), which books each portion's damage under its
    /// own type (<see cref="BattleStats.TypedDamageDealt"/> / <see cref="BattleStats.TypedDamageExposure"/>); the
    /// (unmodified) proficiency accrual (#1318) then claims each path's XP from those per-type books. So the
    /// authored split ratio <em>becomes</em> the cross-school proficiency contribution weight with <b>no
    /// accrual-code change</b> — the verification this issue exists to make.
    ///
    /// These tests author the spike's example content in-memory (a "Flaming Sword" 60% Physical / 40% Fire and an
    /// even-split "Storm Blade" across Wind / Water / Physical, plus a fire-resistant enemy) because static content
    /// is not source-controlled — it lives in the admin Workbench / DB (source-controlled seed/export tooling is
    /// the separate spike #1390). They drive the real <see cref="BattleContext"/> to produce genuine per-portion
    /// books, then run the DB-backed <see cref="ProficiencyRewardService"/> over those books, so the whole chain —
    /// pipeline → typed books → per-path XP — is exercised against live reference data exactly as a won battle does.
    /// The per-portion battle math itself is pinned separately by the BattleContext multi-portion suite (#1385).
    /// </summary>
    [Collection("Integration")]
    public class MultiTypedDamageProficiencyTests : ApplicationIntegrationTestBase
    {
        public MultiTypedDamageProficiencyTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper) { }

        [Fact]
        public async Task FlamingSwordOffense_TrainsEachPortionsPathInProportionToItsSplit()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Offense paths for the two schools the Flaming Sword spans. No Elemental umbrella is seeded, so the
            // Fire portion routes only to the Fire path here (applies(Fire) = [Fire, Elemental] would also feed an
            // Elemental path — covered by the umbrella tests).
            var physical = await CreateKeyedTierAsync(context, EActivityKey.Physical, "Swordsmanship");
            var fire = await CreateKeyedTierAsync(context, EActivityKey.Fire, "Fire Magic");
            var playerId = await SeedPlayerAsync(context);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            // A raw-100 Flaming Sword hit against an unresisting enemy: the offense book is 60 Physical / 40 Fire —
            // the authored split, undisturbed.
            var stats = FirePlayerHit(raw: 100, FlamingSword, enemyFireResistance: 0);
            Assert.Equal(60, TypedDealt(stats, EDamageType.Physical), 0.001);
            Assert.Equal(40, TypedDealt(stats, EDamageType.Fire), 0.001);

            var accrual = await AccrueAsync(scope, playerId, stats);

            // Each path claims pie × clamp(book ÷ power): Physical 60/100 → 6.0, Fire 40/100 → 4.0. The XP ratio is
            // exactly the authored 60:40 split — the split ratio is the cross-school contribution weight, with no
            // accrual change.
            Assert.Equal(ExpectedXp(60), XpFor(accrual, physical));
            Assert.Equal(ExpectedXp(40), XpFor(accrual, fire));
        }

        [Fact]
        public async Task FlamingSwordOffense_AgainstAFireResistantEnemy_TrainsTheFirePathLess()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var physical = await CreateKeyedTierAsync(context, EActivityKey.Physical, "Swordsmanship");
            var fire = await CreateKeyedTierAsync(context, EActivityKey.Fire, "Fire Magic");
            var playerId = await SeedPlayerAsync(context);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            // The same hit against an enemy that resists Fire (0.5) but not Physical: the offense book is post-
            // mitigation, so the Fire portion's net halves to 20 while Physical is unchanged at 60 — the per-portion
            // resistance the cross-school payoff turns on (the enemy "resists one of a hybrid skill's types").
            var stats = FirePlayerHit(raw: 100, FlamingSword, enemyFireResistance: 0.5);
            Assert.Equal(60, TypedDealt(stats, EDamageType.Physical), 0.001);
            Assert.Equal(20, TypedDealt(stats, EDamageType.Fire), 0.001);

            var accrual = await AccrueAsync(scope, playerId, stats);

            // Physical trains the same 6.0; Fire trains half as much (20/100 → 2.0) as against the neutral enemy —
            // the resisted portion contributes, and therefore trains, less.
            Assert.Equal(ExpectedXp(60), XpFor(accrual, physical));
            Assert.Equal(ExpectedXp(20), XpFor(accrual, fire));
        }

        [Fact]
        public async Task StormBladeOffense_EvenSplitTrainsAllThreePathsEqually()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // The even-split Storm Blade exercises a 3-portion hit. Each leaf gets a third of the raw damage, so
            // each of its three paths trains equally — an even split is just equal weights.
            var wind = await CreateKeyedTierAsync(context, EActivityKey.Wind, "Aeromancy");
            var water = await CreateKeyedTierAsync(context, EActivityKey.Water, "Hydromancy");
            var physical = await CreateKeyedTierAsync(context, EActivityKey.Physical, "Swordsmanship");
            var playerId = await SeedPlayerAsync(context);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            // Raw 90 split evenly → 30 Wind / 30 Water / 30 Physical (no resistance).
            var stats = FirePlayerHit(raw: 90, StormBlade, enemyFireResistance: 0);
            Assert.Equal(30, TypedDealt(stats, EDamageType.Wind), 0.001);
            Assert.Equal(30, TypedDealt(stats, EDamageType.Water), 0.001);
            Assert.Equal(30, TypedDealt(stats, EDamageType.Physical), 0.001);

            var accrual = await AccrueAsync(scope, playerId, stats);

            // All three paths claim the identical 30/100 → 3.0 — equal weights, equal training.
            Assert.Equal(ExpectedXp(30), XpFor(accrual, wind));
            Assert.Equal(ExpectedXp(30), XpFor(accrual, water));
            Assert.Equal(ExpectedXp(30), XpFor(accrual, physical));
        }

        [Fact]
        public async Task EnemyFlamingSword_TrainsThePlayersResistPathsPerPortion()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Enemies field the same skills, so multi-typed enemy combat comes free: an enemy Flaming Sword hit
            // exposes the player to each portion's pre-mitigation typed damage, training the matching resist paths.
            var physicalResist = await CreateKeyedTierAsync(context, EActivityKey.PhysicalResist, "Physical Warding");
            var fireResist = await CreateKeyedTierAsync(context, EActivityKey.FireResist, "Fire Warding");
            var playerId = await SeedPlayerAsync(context);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            // The incoming book's exposure is pre-resist regardless of how much the player mitigates: exposure is
            // 60 Physical / 40 Fire whether or not the player has any resistance (#1454's split, below, is what
            // then weights it into a training claim).
            var stats = FireEnemyHit(raw: 100, FlamingSword);
            Assert.Equal(60, Exposure(stats, EDamageType.Physical), 0.001);
            Assert.Equal(40, Exposure(stats, EDamageType.Fire), 0.001);

            var accrual = await AccrueAsync(scope, playerId, stats);

            // A resistance-less player blocks nothing, so both portions' exposure is entirely "unmitigated" and
            // trains at ResistUnmitigatedTrainingRate — still in the same 60:40 proportion as the exposure split.
            Assert.Equal(ExpectedXp(60 * ServerGameConstants.ResistUnmitigatedTrainingRate), XpFor(accrual, physicalResist));
            Assert.Equal(ExpectedXp(40 * ServerGameConstants.ResistUnmitigatedTrainingRate), XpFor(accrual, fireResist));
        }

        [Fact]
        public async Task EnemyFlamingSword_AgainstAResistantPlayer_TrainsThatPortionsResistPathFaster()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            // Same enemy hit, but the player now carries Fire resistance (and none against Physical) — the
            // resist-training split (#1454) should train Fire-resist faster than Physical-resist even though the
            // exposure split (60 Physical / 40 Fire) favors Physical.
            var physicalResist = await CreateKeyedTierAsync(context, EActivityKey.PhysicalResist, "Physical Warding");
            var fireResist = await CreateKeyedTierAsync(context, EActivityKey.FireResist, "Fire Warding");
            var playerId = await SeedPlayerAsync(context);
            await ReferenceCacheReloader.ReloadAllAsync(scope.ServiceProvider);

            var stats = FireEnemyHit(raw: 100, FlamingSword, playerFireResistance: 0.5);
            Assert.Equal(60, Exposure(stats, EDamageType.Physical), 0.001);
            Assert.Equal(40, Exposure(stats, EDamageType.Fire), 0.001);
            Assert.Equal(0, Mitigated(stats, EDamageType.Physical), 0.001);
            Assert.Equal(20, Mitigated(stats, EDamageType.Fire), 0.001);

            var accrual = await AccrueAsync(scope, playerId, stats);

            // Physical: fully unmitigated → 60 × 0.25 = 15 activity. Fire: half mitigated, half not →
            // 20 × 1.0 + 20 × 0.25 = 25 activity — more than Physical despite the smaller exposure share.
            Assert.Equal(ExpectedXp(60 * ServerGameConstants.ResistUnmitigatedTrainingRate), XpFor(accrual, physicalResist));
            Assert.Equal(
                ExpectedXp(20 * ServerGameConstants.ResistMitigatedTrainingRate + 20 * ServerGameConstants.ResistUnmitigatedTrainingRate),
                XpFor(accrual, fireResist));
            Assert.True(XpFor(accrual, fireResist) > XpFor(accrual, physicalResist));
        }

        // ── The example content (#1343 part D) ──────────────────────────────────

        // A flaming sword: 60% Physical, 40% Fire (the spike's worked example). Generic Physical (not a weapon
        // leaf) keeps the routing to a single Physical key, isolating the cross-school split from weapon-gate /
        // Physical-umbrella concerns covered elsewhere.
        private static readonly IReadOnlyList<SkillDamagePortion> FlamingSword = Portions(
            (EDamageType.Physical, 60), (EDamageType.Fire, 40));

        // A storm blade evenly split across three schools (the spike's second example) — equal weights.
        private static readonly IReadOnlyList<SkillDamagePortion> StormBlade = Portions(
            (EDamageType.Wind, 1), (EDamageType.Water, 1), (EDamageType.Physical, 1));

        // ── Battle-pipeline harness (real BattleContext) ────────────────────────

        // The player's power the accrual normalizes each path's activity by. Fixed at 100 so a portion's book value
        // reads directly as its share of the pie (e.g. a 60-damage portion → 0.6 × pie), keeping the assertions the
        // plain proportions the test is about.
        private const double Power = 100.0;

        // Runs one real player fire of a multi-typed hit through the unmodified battle pipeline and returns the
        // resulting stats (the offense book). The enemy optionally resists Fire so the per-portion mitigation shows
        // up in the Fire portion's net; its MaxHealth (150) exceeds every scenario's net so the hit never kills —
        // the offense book is capped at the health actually removed (#1482), and these tests are about the split,
        // not the overkill cap (pinned by the BattleContext suite).
        private static BattleStats FirePlayerHit(
            double raw, IReadOnlyList<SkillDamagePortion> portions, double enemyFireResistance)
        {
            var player = MakeBattler();
            var enemy = MakeBattler((EAttribute.FireResistance, enemyFireResistance), (EAttribute.MaxHealth, 100));
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.DamageTarget(raw, portions, 0);
            return context.Stats;
        }

        // Runs one real enemy fire of a multi-typed hit at the player and returns the stats (the incoming exposure
        // book). The player never dodges (no DodgeChance), so every portion's pre-resist exposure is recorded.
        // Optionally carries the player's own Fire resistance, so the resist-training split (#1454) has something
        // to mitigate.
        private static BattleStats FireEnemyHit(
            double raw, IReadOnlyList<SkillDamagePortion> portions, double playerFireResistance = 0)
        {
            var player = MakeBattler((EAttribute.FireResistance, playerFireResistance));
            var enemy = MakeBattler();
            var context = new BattleContext(player, enemy, timeDelta: 0, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();
            context.DamageTarget(raw, portions, 0);
            return context.Stats;
        }

        private static Battler MakeBattler(params (EAttribute Attribute, double Amount)[] attributes)
        {
            var allocations = attributes
                .Select(a => new StatAllocation { Attribute = a.Attribute, Amount = a.Amount })
                .ToList();
            return BattlerFactory.FromPlayer(new PlayerBuilder().WithStatAllocations(allocations).Build());
        }

        // ── Proficiency-accrual harness (DB-backed service) ─────────────────────

        // Accrues the produced battle stats onto a fresh player's progress through the real reward service, exactly
        // as the battle-completion path does, and returns the result for per-path assertions.
        private async Task<ProficiencyAccrualResult> AccrueAsync(IServiceScope scope, int playerId, BattleStats stats)
        {
            var service = scope.ServiceProvider.GetRequiredService<ProficiencyRewardService>();
            var player = await scope.ServiceProvider.GetRequiredService<IPlayerRepository>().GetPlayer(playerId);
            Assert.NotNull(player);
            var progress = await scope.ServiceProvider.GetRequiredService<IPlayerProgressRepository>().Load(player);
            return service.AccrueAndApply(progress, stats, ratingDenominator: Power, player, notify: false);
        }

        private static async Task<int> SeedPlayerAsync(GameContext context)
        {
            var user = await TestDataSeeder.CreateUserAsync(context);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);
            return player.Id;
        }

        // A single-tier path bound to an activity key (offense or resist). A generous curve keeps the small per-
        // battle gains banked at level 0, so XpGained reads as the raw claim under test.
        private static async Task<Game.Infrastructure.Entities.Proficiency> CreateKeyedTierAsync(
            GameContext context, EActivityKey activityKey, string name)
        {
            var path = await TestDataSeeder.CreatePathAsync(context, name: name, activityKey: activityKey);
            return await TestDataSeeder.CreateProficiencyAsync(context, name: name, pathId: path.Id, pathOrdinal: 0);
        }

        // ── Assertion helpers ───────────────────────────────────────────────────

        // The XP a path claims for an activity quantity: pie × activity ÷ ratingDenominator, rounded to the
        // persisted scale exactly as the reward service does — the calculator's formula, with the test's fixed
        // rating denominator (no clamp, spike #1526 Decision 5).
        private static decimal ExpectedXp(double activity) =>
            Math.Round(
                (decimal)(ServerGameConstants.ProficiencyXpPerVictory * activity / Power),
                3,
                MidpointRounding.AwayFromZero);

        private static decimal XpFor(ProficiencyAccrualResult accrual, Game.Infrastructure.Entities.Proficiency proficiency) =>
            Assert.Single(accrual.Results, r => r.ProficiencyId == proficiency.Id).XpGained;

        private static IReadOnlyList<SkillDamagePortion> Portions(params (EDamageType Type, double Weight)[] portions) =>
            portions.Select(p => new SkillDamagePortion { Type = p.Type, Weight = p.Weight }).ToList();

        private static double TypedDealt(BattleStats stats, EDamageType type) =>
            stats.TypedDamageDealt.TryGetValue(type, out var value) ? value : 0;

        private static double Exposure(BattleStats stats, EDamageType type) =>
            stats.TypedDamageExposure.TryGetValue(type, out var value) ? value : 0;

        private static double Mitigated(BattleStats stats, EDamageType type) =>
            stats.TypedDamageResistanceMitigated.TryGetValue(type, out var value) ? value : 0;
    }
}
