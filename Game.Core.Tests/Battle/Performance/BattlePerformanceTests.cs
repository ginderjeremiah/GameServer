using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Skills;
using Xunit;

namespace Game.Core.Tests.Battle.Performance
{
    /// <summary>
    /// Performance guard-rails for the backend battle simulation (issue #283).
    /// <para>
    /// The backend re-simulates every battle <em>after</em> the client reports it (the anti-cheat
    /// replay), so the simulation hot path must stay cheap as new mechanics are added. These tests
    /// protect that by measuring <em>relatively</em> rather than against absolute times, because raw
    /// machine speed varies wildly between dev boxes and CI runners:
    /// </para>
    /// <list type="bullet">
    ///   <item><b>Scaling gates (primary, machine-independent).</b> A simple baseline battle is
    ///   compared against a more complex one that differs in exactly one dimension (skill count, then
    ///   damage-multiplier count) by a known factor. Because both run on the same machine in the same
    ///   run, the ratio cancels out machine speed; what it actually protects is that added work scales
    ///   roughly <em>linearly</em>, not super-linearly. A regression that turns a per-skill or
    ///   per-multiplier loop into an accidental O(n²) blows far past the budget while ordinary CI
    ///   noise cannot.</item>
    ///   <item><b>Throughput backstop (coarse, machine-dependent).</b> One deliberately generous
    ///   absolute ceiling on the worst-case battle catches catastrophic <em>uniform</em> regressions
    ///   (e.g. a new per-tick allocation that inflates every battle equally — which a ratio cannot
    ///   see). Its margin is large enough to never flake on a slow runner; subtle regressions are
    ///   meant to be spotted by watching the logged numbers, not by this gate.</item>
    /// </list>
    /// <para>
    /// Every measurement also logs its raw figures via <see cref="ITestOutputHelper"/> so the actual
    /// costs/ratios can be tracked over time and the thresholds tightened from data.
    /// </para>
    /// <para>
    /// These are tagged <c>[Trait("Category", "Performance")]</c> so they can be excluded from a run
    /// in one line (<c>dotnet test -- --filter-not-trait "Category=Performance"</c>) if they ever prove
    /// noisy on the PR gate. They are intentionally <b>backend-only</b>: unlike the battle-simulation
    /// <em>parity</em> tests, they assert nothing about correctness or determinism, so there is no
    /// frontend mirror to keep in sync.
    /// </para>
    /// </summary>
    [Trait("Category", "Performance")]
    public class BattlePerformanceTests
    {
        // Per-tick cost cancels the tick count, so the scaling gates use a modest fixed length to stay
        // fast; the backstop uses the simulator's real default cap (DefaultMaxBattleMs / MsPerTick = 10000).
        private const int ScalingTicks = 800;
        private const int WorstCaseTicks = 10_000;

        // Sampling budget — large enough that the Min statistic is stable run-to-run, small enough to
        // keep the whole class to a couple of seconds even on a slow CI runner.
        private const int WarmupIterations = 20;
        private const int SampleCount = 15;
        private const int OperationsPerSample = 25;

        // Scenario shape. "Complex" differs from "simple" by an 8x factor in exactly one dimension.
        private const int SimpleSkillCount = 1;
        private const int ComplexSkillCount = 8;
        private const int SimpleMultiplierCount = 1;
        private const int ComplexMultiplierCount = 8;

        // Scaling from 1 to 8 work units should land near 8x (and, thanks to shared per-tick overhead,
        // usually below it). The tolerance gives generous headroom for CI noise and cache effects
        // while still catching a super-linear blow-up — a quadratic regression at this factor is ~64x,
        // far above 8 * 2.0 = 16x.
        private const double LinearScalingTolerance = 2.0;

        // Coarse catastrophic-regression ceiling for one worst-case battle (8 skills/side, 8
        // multipliers each, run to the full default tick cap). This is the one machine-dependent gate
        // (a uniform per-tick regression inflates every battle equally, which a ratio cannot see), so
        // its margin is deliberately huge: a slow CI runner measures this battle in tens of
        // milliseconds, an order of magnitude under the ceiling, while a regression that makes the
        // per-tick path ~10x slower still trips it. If extreme runner contention ever makes even this
        // flake, the Performance trait excludes it from the gate in one line.
        private const double WorstCaseCeilingMs = 500.0;

        private readonly ITestOutputHelper _output;

        public BattlePerformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void SkillCountScaling_StaysWithinLinearBounds()
        {
            var simplePerTick = MeasurePerTick("1 skill / side", SimpleSkillCount, SimpleMultiplierCount);
            var complexPerTick = MeasurePerTick("8 skills / side", ComplexSkillCount, SimpleMultiplierCount);

            var ratio = complexPerTick / simplePerTick;
            var workFactor = (double)ComplexSkillCount / SimpleSkillCount;
            var threshold = workFactor * LinearScalingTolerance;

            _output.WriteLine(
                $"Skill-count per-tick ratio: {ratio:F2}x for {workFactor:F0}x the skills (budget {threshold:F1}x)");

            Assert.True(
                ratio <= threshold,
                $"Battle simulation per-tick cost scaled {ratio:F2}x going from {SimpleSkillCount} to "
                + $"{ComplexSkillCount} skills/side, exceeding the {threshold:F1}x linear-scaling budget. "
                + "This points at super-linear cost growth in the per-skill battle path.");
        }

        [Fact]
        public void DamageMultiplierScaling_StaysWithinLinearBounds()
        {
            var simplePerTick = MeasurePerTick("1 multiplier / skill", SimpleSkillCount, SimpleMultiplierCount);
            var complexPerTick = MeasurePerTick("8 multipliers / skill", SimpleSkillCount, ComplexMultiplierCount);

            var ratio = complexPerTick / simplePerTick;
            var workFactor = (double)ComplexMultiplierCount / SimpleMultiplierCount;
            var threshold = workFactor * LinearScalingTolerance;

            _output.WriteLine(
                $"Multiplier per-tick ratio: {ratio:F2}x for {workFactor:F0}x the multipliers (budget {threshold:F1}x)");

            Assert.True(
                ratio <= threshold,
                $"Battle simulation per-tick cost scaled {ratio:F2}x going from {SimpleMultiplierCount} to "
                + $"{ComplexMultiplierCount} damage multipliers/skill, exceeding the {threshold:F1}x "
                + "linear-scaling budget. This points at super-linear cost growth in "
                + $"{nameof(BattleSkill)}.{nameof(BattleSkill.CalculateDamage)}.");
        }

        [Fact]
        public void WorstCaseBattle_SimulatesWellWithinRealtimeBudget()
        {
            var result = Measure(
                "worst case (8 skills x 8 multipliers, full battle)",
                ComplexSkillCount,
                ComplexMultiplierCount,
                WorstCaseTicks,
                warmup: 3,
                samples: 6,
                operationsPerSample: 3);

            var minMs = result.MinMicroseconds / 1000.0;
            _output.WriteLine(
                $"Worst-case full battle: {minMs:F3} ms (min), {result.MedianMicroseconds / 1000.0:F3} ms (median) "
                + $"over {WorstCaseTicks} ticks; ceiling {WorstCaseCeilingMs:F0} ms");

            Assert.True(
                minMs < WorstCaseCeilingMs,
                $"Worst-case battle simulated in {minMs:F3} ms, exceeding the coarse {WorstCaseCeilingMs:F0} ms "
                + "backstop. The simulation should be far faster than this; a breach indicates a large uniform "
                + "performance regression in the per-tick path.");
        }

        /// <summary>
        /// Measures a battle configuration and returns its cost normalised to microseconds per tick,
        /// logging the raw figures. Per-tick normalisation lets battles of different lengths be
        /// compared and keeps the ratio independent of the chosen tick count.
        /// </summary>
        private double MeasurePerTick(string label, int skillCount, int multiplierCount)
        {
            var result = Measure(
                label, skillCount, multiplierCount, ScalingTicks,
                WarmupIterations, SampleCount, OperationsPerSample);

            var perTickNanoseconds = result.MinMicroseconds / ScalingTicks * 1000.0;
            _output.WriteLine(
                $"  {label}: {result.MinMicroseconds:F2} us/battle (min), {perTickNanoseconds:F2} ns/tick; "
                + $"median {result.MedianMicroseconds:F2} us, mean {result.MeanMicroseconds:F2} us");

            return result.MinMicroseconds / ScalingTicks;
        }

        private MeasurementResult Measure(
            string label, int skillCount, int multiplierCount, int ticks,
            int warmup, int samples, int operationsPerSample)
        {
            var maxMs = ticks * GameConstants.MsPerTick;

            // Validate the scenario really runs to the tick cap: both combatants must survive so the
            // battle does a fixed, known amount of per-tick work (required for the per-tick figures to
            // mean anything). A scenario that ended early would silently measure a different workload.
            var probe = CreateSimulator(skillCount, multiplierCount).Simulate(maxMs);
            Assert.False(probe.Victory, $"{label}: expected a run-to-the-cap battle, but the player won.");
            Assert.False(probe.PlayerDied, $"{label}: expected a run-to-the-cap battle, but the player died.");
            Assert.Equal(maxMs, probe.TotalMs);

            return PerformanceMeasurement.Measure(
                createInput: () => CreateSimulator(skillCount, multiplierCount),
                timedOperation: simulator => simulator.Simulate(maxMs),
                warmupIterations: warmup,
                sampleCount: samples,
                operationsPerSample: operationsPerSample);
        }

        private static BattleSimulator CreateSimulator(int skillCount, int multiplierCount)
        {
            return new BattleSimulator(
                BuildBattler(skillCount, multiplierCount),
                BuildBattler(skillCount, multiplierCount));
        }

        /// <summary>
        /// Builds a combatant that deliberately never dies — high Endurance yields enormous MaxHealth
        /// and a Defense that clamps every incoming hit to zero — so the battle deterministically runs
        /// to the tick cap. Skills still fire on every cooldown, so the per-tick hot path (charge,
        /// <see cref="BattleSkill.CalculateDamage"/>, stat recording) is fully exercised; only the
        /// life-total bookkeeping is neutralised.
        /// </summary>
        private static Battler BuildBattler(int skillCount, int multiplierCount)
        {
            var attributes = new AttributeCollection(
            [
                Modifier(EAttribute.Strength, 100),
                Modifier(EAttribute.Endurance, 1000),
            ]);

            var skills = new List<Skill>(skillCount);
            for (var id = 0; id < skillCount; id++)
            {
                skills.Add(BuildSkill(id, multiplierCount));
            }

            return new Battler(attributes, skills, level: 1);
        }

        private static Skill BuildSkill(int id, int multiplierCount)
        {
            var multipliers = new List<AttributeModifier>(multiplierCount);
            for (var i = 0; i < multiplierCount; i++)
            {
                multipliers.Add(new AttributeModifier
                {
                    Attribute = EAttribute.Strength,
                    Amount = 0.5,
                    Type = EModifierType.Multiplicative,
                    Source = EAttributeModifierSource.Derived,
                });
            }

            return new Skill
            {
                Id = id,
                Name = $"Skill {id}",
                Description = string.Empty,
                CooldownMs = 80, // fires every couple of ticks, so the damage path runs heavily
                BaseDamage = 10,
                DamageMultipliers = multipliers,
            };
        }

        private static AttributeModifier Modifier(EAttribute attribute, double amount) => new()
        {
            Attribute = attribute,
            Amount = amount,
            Type = EModifierType.Additive,
            Source = EAttributeModifierSource.AttributeDistribution,
        };
    }
}
