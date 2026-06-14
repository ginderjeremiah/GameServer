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
    ///   absolute ceiling on the realistic worst-case battle (the full 4-skill loadout with effects, run
    ///   to the tick cap) catches catastrophic <em>uniform</em> regressions (e.g. a new per-tick
    ///   allocation that inflates every battle equally — which a ratio cannot see). Its margin is large
    ///   enough to never flake on a slow runner; subtle regressions are meant to be spotted by watching
    ///   the logged numbers, not by this gate.</item>
    /// </list>
    /// <para>
    /// Scenarios reflect the real loadout cap (<see cref="GameConstants.MaxSelectedSkills"/> = 4 skills,
    /// 1–2 multipliers each, a handful of active effects). The 8× <em>scaling</em> configs below are a
    /// deliberate sensitivity stressor for the ratio gates, not a claim that such a loadout occurs.
    /// </para>
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

        // Representative "typical" battle for the current feature set (per design discussion): a full
        // 4-skill loadout per side with a couple of damage multipliers each, a handful of concurrent
        // active effects, and a ~30s fight. The worst case reuses this loadout but runs to the
        // simulation's hard tick cap (a fight neither side can finish) — uncommon, but it must stay
        // tolerable, especially as the server may be slower than a dev box and concurrent CPU-bound
        // simulations contend where async I/O would overlap.
        private const int TypicalSkillCount = 4;            // == GameConstants.MaxSelectedSkills
        private const int TypicalMultiplierCount = 2;       // upper end of the "1 or 2" estimate
        private const int TypicalEffectsPerSkill = 1;       // 4 skills x 1 => ~4 concurrent active effects
        private const int TypicalBattleTicks = 750;         // ~30s at MsPerTick = 40

        // Coarse catastrophic-regression ceiling for the realistic worst-case battle (the typical full
        // loadout — 4 skills, 2 multipliers + an effect each — run to the 10,000-tick cap). This is the
        // one machine-dependent gate: a uniform per-tick regression inflates every battle equally, which
        // the ratio gates cannot see. The margin is deliberately huge (the battle measures ~10ms on a dev
        // box, an order of magnitude under the ceiling); if extreme runner contention ever makes it flake,
        // the Performance trait excludes it in one line.
        private const double MaxLengthRealisticCeilingMs = 250.0;

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
        public void TypicalBattle_SimulationCost()
        {
            var result = Measure(
                "typical (4 skills x 2 multipliers, ~4 active effects, ~30s)",
                TypicalSkillCount, TypicalMultiplierCount, TypicalBattleTicks,
                WarmupIterations, SampleCount, OperationsPerSample, TypicalEffectsPerSkill);

            var perTickNanoseconds = result.MinMicroseconds / TypicalBattleTicks * 1000.0;
            _output.WriteLine(
                $"Typical battle: {result.MinMicroseconds / 1000.0:F3} ms (min), "
                + $"{result.MedianMicroseconds / 1000.0:F3} ms (median) over {TypicalBattleTicks} ticks; "
                + $"{perTickNanoseconds:F1} ns/tick");
        }

        /// <summary>
        /// The realistic worst case for the current feature set: the full typical loadout (4 skills, 2
        /// multipliers + an effect each) run to the simulation's hard tick cap — a fight neither side can
        /// finish. This deliberately replaces an earlier contrived 8-skill x 8-multiplier scenario that
        /// can never occur in play; an effect on every skill is contrived but actually reachable. Doubles
        /// as the coarse uniform-regression backstop (see <see cref="MaxLengthRealisticCeilingMs"/>).
        /// </summary>
        [Fact]
        public void WorstCaseBattle_StaysTolerable()
        {
            var result = Measure(
                "worst case (4 skills x 2 multipliers, an effect each, full cap)",
                TypicalSkillCount, TypicalMultiplierCount, WorstCaseTicks,
                warmup: 3, samples: 6, operationsPerSample: 3, effectsPerSkill: TypicalEffectsPerSkill);

            var minMs = result.MinMicroseconds / 1000.0;
            _output.WriteLine(
                $"Worst-case realistic battle: {minMs:F3} ms (min), {result.MedianMicroseconds / 1000.0:F3} ms "
                + $"(median) over {WorstCaseTicks} ticks; ceiling {MaxLengthRealisticCeilingMs:F0} ms");

            Assert.True(
                minMs < MaxLengthRealisticCeilingMs,
                $"Worst-case realistic battle simulated in {minMs:F3} ms, exceeding the "
                + $"{MaxLengthRealisticCeilingMs:F0} ms tolerance for a full-cap battle with a realistic loadout.");
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
            int warmup, int samples, int operationsPerSample, int effectsPerSkill = 0)
        {
            var maxMs = ticks * GameConstants.MsPerTick;

            // Validate the scenario really runs to the tick cap: both combatants must survive so the
            // battle does a fixed, known amount of per-tick work (required for the per-tick figures to
            // mean anything). A scenario that ended early would silently measure a different workload.
            var probe = CreateSimulator(skillCount, multiplierCount, effectsPerSkill).Simulate(maxMs);
            Assert.False(probe.Victory, $"{label}: expected a run-to-the-cap battle, but the player won.");
            Assert.False(probe.PlayerDied, $"{label}: expected a run-to-the-cap battle, but the player died.");
            Assert.Equal(maxMs, probe.TotalMs);

            return PerformanceMeasurement.Measure(
                createInput: () => CreateSimulator(skillCount, multiplierCount, effectsPerSkill),
                timedOperation: simulator => simulator.Simulate(maxMs),
                warmupIterations: warmup,
                sampleCount: samples,
                operationsPerSample: operationsPerSample);
        }

        private static BattleSimulator CreateSimulator(int skillCount, int multiplierCount, int effectsPerSkill = 0)
        {
            return new BattleSimulator(
                BuildBattler(skillCount, multiplierCount, effectsPerSkill),
                BuildBattler(skillCount, multiplierCount, effectsPerSkill));
        }

        /// <summary>
        /// Builds a combatant that deliberately never dies — high Endurance yields enormous MaxHealth
        /// and a Defense that clamps every incoming hit to zero — so the battle deterministically runs
        /// to the tick cap. Skills still fire on every cooldown, so the per-tick hot path (charge,
        /// <see cref="BattleSkill.CalculateDamage"/>, stat recording) is fully exercised; only the
        /// life-total bookkeeping is neutralised.
        /// </summary>
        private static Battler BuildBattler(int skillCount, int multiplierCount, int effectsPerSkill = 0)
        {
            var attributes = new AttributeCollection(
            [
                Modifier(EAttribute.Strength, 100),
                Modifier(EAttribute.Endurance, 1000),
            ]);

            var skills = new List<Skill>(skillCount);
            for (var id = 0; id < skillCount; id++)
            {
                skills.Add(BuildSkill(id, multiplierCount, effectsPerSkill));
            }

            return new Battler(attributes, skills, level: 1);
        }

        private static Skill BuildSkill(int id, int multiplierCount, int effectsPerSkill = 0)
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

            var effects = new List<SkillEffect>(effectsPerSkill);
            for (var i = 0; i < effectsPerSkill; i++)
            {
                // A self-targeted buff whose duration spans many cooldowns: once each skill has fired, its
                // effect stays continuously active (refreshed on every fire), so the per-tick AdvanceEffects
                // pass and the buffed-attribute cascade are exercised the way a real loadout would.
                effects.Add(new SkillEffect
                {
                    Id = (id * 10) + i,
                    Target = ESkillEffectTarget.Self,
                    AttributeId = EAttribute.Strength,
                    ModifierType = EModifierType.Additive,
                    Amount = 5,
                    DurationMs = 400,
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
                Effects = effects,
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
