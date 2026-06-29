using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Skills;
using Xunit;
using static Game.Core.EAttribute;

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
    ///   absolute ceiling on the realistic worst-case battle (the full 4-skill loadout with churning
    ///   effects, run to the tick cap) catches catastrophic <em>uniform</em> regressions (e.g. a new
    ///   per-tick allocation that inflates every battle equally — which a ratio cannot see). Its margin
    ///   is large enough to never flake on a slow runner; subtle regressions are meant to be spotted by
    ///   watching the logged numbers, not by this gate.</item>
    ///   <item><b>Attribute-cache observability.</b> The same typical loadout is measured with both
    ///   <em>persistent</em> effects (each fire folds another application into the attribute's single combined
    ///   modifier and resets the shared per-attribute expiry, so the magnitude accumulates without lapsing
    ///   while the modifier count stays flat) and
    ///   <em>churning</em> effects (every cooldown cycle expires and re-applies each effect, invalidating
    ///   those nodes and the derived attributes that cascade from them — the recompute path the
    ///   <see cref="AttributeCollection"/> cache exists to amortise). The per-skill-fire cost of each is logged
    ///   (normalised per fire, not per tick, because the persistent buff ramps CooldownRecovery and so fires
    ///   far more often — see that test) so the cache's per-event cost is visible and trackable; both are
    ///   <em>asserted</em> to genuinely exercise the cache by
    ///   <see cref="ChurningEffectsExpireAndReapply_WhilePersistentEffectsStack"/>, and the per-tick cost is
    ///   held flat as the battle lengthens by
    ///   <see cref="PersistentStacking_PerTickCostStaysFlatAsBattleLengthens"/>.</item>
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
        private const int WorstCaseTicks = GameConstants.DefaultMaxBattleMs / GameConstants.MsPerTick;

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
        // loadout — 4 skills, 2 multipliers + a churning effect each — run to the 10,000-tick cap). This is
        // the one machine-dependent gate: a uniform per-tick regression inflates every battle equally, which
        // the ratio gates cannot see. The margin is deliberately huge (the battle measures ~10ms on a dev
        // box, an order of magnitude under the ceiling); if extreme runner contention ever makes it flake,
        // the Performance trait excludes it in one line.
        private const double MaxLengthRealisticCeilingMs = 250.0;

        // Persistent-stacking flatness gate. A persistently re-applied buff folds into one combined modifier
        // per (attribute, type), so a battle's per-tick cost must stay flat as it lengthens; the pre-fold
        // per-application list made per-tick cost rise linearly with length (an O(ticks²) battle). The 4x
        // length spread separates the two cleanly — a flat (folded) cost lands near 1x (and below, as the
        // per-battle setup amortises over more ticks), while the quadratic regression lands near 4x — so the
        // generous 2.0x budget catches the regression without ever flaking on the healthy case.
        private const int StackingShortTicks = 400;
        private const int StackingLongTicks = 1600;
        private const double StackingFlatnessBudget = 2.0;

        // Combatant base spread. The skills deal no damage (zero base, zero-weight multipliers), so neither
        // combatant can die and the battle deterministically runs to the tick cap — the Toughness curve never
        // fully clamps a real hit, so immortality comes from the zero damage, not from mitigation. The large
        // Endurance still composes a realistic MaxHealth/Toughness for the per-tick work.
        private const int BattlerBaseStrength = 100;
        private const int BattlerBaseEndurance = 1000;

        // Skills fire every other tick (CooldownMs = 80, MsPerTick = 40). The two effect modes differ only
        // in how their DurationMs compares to that cooldown, which shapes how the attribute cache churns:
        //   - Persistent: DurationMs > cooldown, so each fire STACKS another application AND resets the shared
        //     per-attribute expiry (#992/#740) before any application would lapse — nothing expires during the
        //     run. Each fire folds the new application into the attribute's single combined modifier (a swap),
        //     so the buffed node is invalidated each fire and its derived dependents recompute over a bounded
        //     modifier set — the magnitude accumulates while the modifier COUNT stays flat, keeping a long
        //     battle O(ticks) rather than the O(ticks²) a per-application modifier list produced.
        //   - Churning:   DurationMs < cooldown, so the effect EXPIRES every cycle (RemoveModifier) and is
        //     re-applied on the next fire (AddModifier). Each add/remove invalidates the buffed node and
        //     cascades to its derived dependents, so the per-tick reads of those derived attributes pay the
        //     recompute the cache exists to avoid.
        private const int SkillCooldownMs = 80;
        private const int PersistentEffectDurationMs = 400;   // > cooldown: stacks before any application expires
        private const int ChurningEffectDurationMs = 40;      // one tick: applied on a fire, gone before the next
        private const int EffectBuffAmount = 5;

        // Effects rotate across the core attributes that actually have derived dependents, so a churn
        // invalidation cascades into the attributes read on the hot path: Strength→MaxHealth,
        // Endurance→MaxHealth/Toughness, Agility→CooldownRecovery/DodgeChance,
        // Dexterity→CooldownRecovery/CriticalChance. A 4-skill loadout therefore churns every derived
        // attribute read each tick (MaxHealth for the death check, CooldownRecovery for charge, the crit/dodge
        // chances on each fire). Intellect/Luck are omitted because nothing the hot path reads derives from
        // them. The buffs are additive and Self-targeted, so they only ever raise those stats — and the skills
        // deal no damage regardless, so the combatant stays immortal and the battle runs to the tick cap.
        private static readonly EAttribute[] BuffableCoreAttributes = [Strength, Endurance, Agility, Dexterity];

        private enum EffectMode
        {
            None,
            Persistent,
            Churning,
        }

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

        /// <summary>
        /// Observability for the <see cref="AttributeCollection"/> cache cost. Measures the typical loadout
        /// twice — once with <em>persistent</em> effects (each fire folds into the attribute's single combined
        /// modifier, so the buffed node is invalidated per fire over a bounded modifier set) and once with
        /// <em>churning</em> effects (each cooldown cycle expires and re-applies every effect, so the buffed
        /// core attributes and their derived dependents are invalidated and recomputed on the hot path) — and
        /// logs the cost <b>per skill-fire</b>, not per tick, because the two scenarios do <em>not</em> fire the
        /// same number of times over a battle. The persistent buffs rotate onto Agility/Dexterity, which feed
        /// <c>CooldownRecovery</c>; because they never lapse, CooldownRecovery ramps and the fire rate
        /// accelerates to the per-tick cap, so a persistent battle fires ~2x as often as a churning one. Per
        /// <em>tick</em>, that fire-count gap swamps (and inverts) the cache cost — the figure of interest — so
        /// normalising per fire divides it out and surfaces the genuine per-event cost, where churning's
        /// expire→recompute round-trip is the dearer one. This is deliberately logged, not asserted: the
        /// recompute touches only small nodes, so the absolute overhead is small and a tight ratio gate would
        /// flake; the figure is here to be trend-watched and to make the cache's cost visible. The mechanism
        /// that makes each scenario genuinely exercise the cache is pinned deterministically by
        /// <see cref="ChurningEffectsExpireAndReapply_WhilePersistentEffectsStack"/>, the per-tick cost is held
        /// flat as the battle lengthens by <see cref="PersistentStacking_PerTickCostStaysFlatAsBattleLengthens"/>,
        /// and a catastrophic regression would also breach <see cref="WorstCaseBattle_StaysTolerable"/>.
        /// </summary>
        [Fact]
        public void TypicalBattle_PersistentVsChurningEffects_LogsCacheChurnCost()
        {
            var persistent = Measure(
                "typical, persistent effects (stacking)",
                TypicalSkillCount, TypicalMultiplierCount, TypicalBattleTicks,
                WarmupIterations, SampleCount, OperationsPerSample, TypicalEffectsPerSkill, EffectMode.Persistent);

            var churn = Measure(
                "typical, churning effects (cache invalidate + recompute each cycle)",
                TypicalSkillCount, TypicalMultiplierCount, TypicalBattleTicks,
                WarmupIterations, SampleCount, OperationsPerSample, TypicalEffectsPerSkill, EffectMode.Churning);

            // Normalise per skill-fire, not per tick: the persistent buff ramps CooldownRecovery and so fires
            // far more often than the churning one, which would otherwise dominate a per-tick figure.
            var persistentFires = CountSkillFires(EffectMode.Persistent);
            var churnFires = CountSkillFires(EffectMode.Churning);
            var persistentPerFireNs = persistent.MinMicroseconds / persistentFires * 1000.0;
            var churnPerFireNs = churn.MinMicroseconds / churnFires * 1000.0;

            _output.WriteLine(
                $"Persistent typical battle:   {persistent.MinMicroseconds / 1000.0:F3} ms (min), {persistentFires} fires, {persistentPerFireNs:F1} ns/fire");
            _output.WriteLine(
                $"Churning typical battle:     {churn.MinMicroseconds / 1000.0:F3} ms (min), {churnFires} fires, {churnPerFireNs:F1} ns/fire");
            _output.WriteLine(
                $"Attribute-cache churn cost:  {churnPerFireNs - persistentPerFireNs:+0.0;-0.0} ns/fire "
                + $"({churnPerFireNs / persistentPerFireNs:F2}x persistent per fire)");
            _output.WriteLine(
                $"  (persistent runs {persistent.MinMicroseconds / churn.MinMicroseconds:F2}x churn's wall-time on "
                + $"{(double)persistentFires / churnFires:F2}x the fires — a CooldownRecovery-ramp work gap, not a cache cost)");
        }

        /// <summary>
        /// Skill-fire count for one full typical battle in the given mode — the work normaliser for
        /// <see cref="TypicalBattle_PersistentVsChurningEffects_LogsCacheChurnCost"/>. The two modes differ
        /// because persistent buffs ramp CooldownRecovery (and so the fire rate); see that test's remarks.
        /// </summary>
        private static int CountSkillFires(EffectMode effectMode)
        {
            var maxMs = TypicalBattleTicks * GameConstants.MsPerTick;
            return CreateSimulator(TypicalSkillCount, TypicalMultiplierCount, TypicalEffectsPerSkill, effectMode)
                .Simulate(maxMs).Stats.PlayerSkillsUsed;
        }

        /// <summary>
        /// Regression gate for the stacking-effect cost (machine-independent). A persistently re-applied buff
        /// folds into one combined modifier per (attribute, type), so a battle's per-tick cost must stay
        /// roughly <em>flat</em> as the battle lengthens. The pre-fold representation added one modifier per
        /// fire, so reads, adds and the expiry scan all grew with the stack depth — per-tick cost rose linearly
        /// with length, making a long battle O(ticks²) (the regression #992 introduced and that silently
        /// inflated this suite's runtime). Measuring the same scenario at two lengths on the same machine
        /// cancels raw speed and isolates the asymptotic growth: a folded cost holds near 1x, the quadratic
        /// regression blows past the budget at ~4x for this 4x length spread.
        /// </summary>
        [Fact]
        public void PersistentStacking_PerTickCostStaysFlatAsBattleLengthens()
        {
            var shortPerTick = MeasureStackingPerTick(StackingShortTicks);
            var longPerTick = MeasureStackingPerTick(StackingLongTicks);

            var ratio = longPerTick / shortPerTick;
            var lengthFactor = (double)StackingLongTicks / StackingShortTicks;

            _output.WriteLine(
                $"Persistent-stacking per-tick cost: {shortPerTick:F2} ns at {StackingShortTicks} ticks, "
                + $"{longPerTick:F2} ns at {StackingLongTicks} ticks → {ratio:F2}x over {lengthFactor:F0}x the "
                + $"length (budget {StackingFlatnessBudget:F1}x)");

            Assert.True(
                ratio <= StackingFlatnessBudget,
                $"Persistent-stacking per-tick cost scaled {ratio:F2}x when the battle length grew "
                + $"{lengthFactor:F0}x ({StackingShortTicks}→{StackingLongTicks} ticks), exceeding the "
                + $"{StackingFlatnessBudget:F1}x flatness budget. A per-tick cost that rises with battle length "
                + "means stacked effects are no longer folding into a single combined modifier per "
                + "(attribute, type) — the O(ticks²) regression this fold exists to prevent.");
        }

        /// <summary>
        /// Per-tick cost (ns) of the typical persistently-stacking scenario at the given battle length. Run to
        /// the tick cap (the buffs keep the combatant immortal), normalised per tick so the figure is
        /// comparable across lengths and only its scaling with length is asserted.
        /// </summary>
        private double MeasureStackingPerTick(int ticks)
        {
            var result = Measure(
                $"persistent stacking, {ticks} ticks",
                TypicalSkillCount, TypicalMultiplierCount, ticks,
                warmup: 5, samples: 8, operationsPerSample: 8,
                effectsPerSkill: TypicalEffectsPerSkill, effectMode: EffectMode.Persistent);

            return result.MinMicroseconds / ticks * 1000.0;
        }

        /// <summary>
        /// The realistic worst case for the current feature set: the full typical loadout (4 skills, 2
        /// multipliers + an effect each) run to the simulation's hard tick cap — a fight neither side can
        /// finish. The effects are <em>churning</em> (see <see cref="EffectMode"/>) so the full-cap battle
        /// also stresses the attribute cache — invalidate + recompute + derived cascade every cooldown cycle
        /// — rather than only the skill/DoT loops. Doubles as the coarse uniform-regression backstop (see
        /// <see cref="MaxLengthRealisticCeilingMs"/>).
        /// </summary>
        [Fact]
        public void WorstCaseBattle_StaysTolerable()
        {
            var result = Measure(
                "worst case (4 skills x 2 multipliers, a churning effect each, full cap)",
                TypicalSkillCount, TypicalMultiplierCount, WorstCaseTicks,
                warmup: 3, samples: 6, operationsPerSample: 3,
                effectsPerSkill: TypicalEffectsPerSkill, effectMode: EffectMode.Churning);

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
        /// Deterministic guard that both effect scenarios genuinely exercise the cache, independent of machine
        /// speed (it asserts attribute <em>values</em>, not timings). With <see cref="EffectMode.Churning"/> the
        /// effect's <see cref="ChurningEffectDurationMs"/> is below the skill cooldown, so a self-buff expires
        /// between fires and is re-applied on the next one — the buffed attribute toggles base → buffed → base
        /// repeatedly, each toggle an <c>AddModifier</c>/<c>RemoveModifier</c> that invalidates the cache. With
        /// <see cref="EffectMode.Persistent"/> the longer duration outlasts the cooldown, so each fire
        /// <em>stacks</em> a fresh application and resets the shared per-attribute expiry before any would lapse
        /// (#992/#740) — Strength ramps monotonically above a single buff and never returns to base, each fire
        /// folding another magnitude into the attribute's combined modifier the recompute path reads. This fails
        /// if a future change to the durations or cooldown silently stops either scenario from exercising the
        /// recompute path.
        /// </summary>
        [Fact]
        public void ChurningEffectsExpireAndReapply_WhilePersistentEffectsStack()
        {
            const int ticks = 12;
            const int buffedStrength = BattlerBaseStrength + EffectBuffAmount;

            var churning = SampleActiveStrengthOverTicks(EffectMode.Churning, ticks);
            var persistent = SampleActiveStrengthOverTicks(EffectMode.Persistent, ticks);

            // Churn: the buff comes and goes, so both the base and buffed Strength values appear repeatedly.
            Assert.Contains(BattlerBaseStrength, churning);
            Assert.Contains(buffedStrength, churning);

            // At least two full expire-then-reapply cycles in the window (a base reading immediately
            // following a buffed one, twice over) — proving repeated invalidation, not a single application.
            Assert.True(
                CountExpiries(churning, buffedStrength, BattlerBaseStrength) >= 2,
                "Expected the churning effect to expire and re-apply repeatedly, but the sampled Strength "
                + $"values were [{string.Join(", ", churning)}].");

            // Persistent: the duration outlasts the cooldown and each fire resets the shared per-attribute
            // expiry, so no application lapses during the run — Strength ramps above a single buff and never
            // returns to base (each fire folds another magnitude into the attribute's combined modifier).
            Assert.True(
                persistent[^1] > buffedStrength,
                "Expected persistent re-application to stack above a single buff, but the sampled Strength "
                + $"values were [{string.Join(", ", persistent)}].");
            Assert.DoesNotContain(BattlerBaseStrength, persistent[1..]); // never returns to base after the first fire
            for (var i = 1; i < persistent.Length; i++)
            {
                Assert.True(persistent[i] >= persistent[i - 1], "Stacked Strength should only ramp upward.");
            }
        }

        /// <summary>
        /// Drives a single-skill battler tick-by-tick (mirroring the simulator's "expire effects, then act"
        /// order) and samples the active battler's Strength after each tick. The skill carries a Self
        /// Strength buff (skill id 0 maps to <see cref="BuffableCoreAttributes"/>[0] = Strength), so the
        /// samples reveal whether the buff is churning (toggling) or persisting (staying buffed).
        /// </summary>
        private static int[] SampleActiveStrengthOverTicks(EffectMode effectMode, int ticks)
        {
            // Built through the same BuildBattler/BuildSkill path the perf scenarios use, so the proof tracks
            // their actual effect shape rather than a bespoke one. No multipliers are needed here.
            var active = BuildBattler(skillCount: 1, multiplierCount: 0, effectsPerSkill: 1, effectMode);
            var target = BuildBattler(skillCount: 1, multiplierCount: 0, effectsPerSkill: 1, effectMode);
            var context = new BattleContext(active, target, GameConstants.MsPerTick, new Mulberry32(0));

            var samples = new int[ticks];
            for (var i = 0; i < ticks; i++)
            {
                active.AdvanceEffects(GameConstants.MsPerTick);
                active.Update(context);
                samples[i] = (int)active.GetAttributeValue(Strength);
            }

            return samples;
        }

        /// <summary>
        /// Counts the transitions from <paramref name="buffedValue"/> to <paramref name="baseValue"/> in
        /// <paramref name="samples"/> — i.e. how many times the effect was observed expiring.
        /// </summary>
        private static int CountExpiries(IReadOnlyList<int> samples, int buffedValue, int baseValue)
        {
            var expiries = 0;
            for (var i = 1; i < samples.Count; i++)
            {
                if (samples[i - 1] == buffedValue && samples[i] == baseValue)
                {
                    expiries++;
                }
            }

            return expiries;
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
            int warmup, int samples, int operationsPerSample,
            int effectsPerSkill = 0, EffectMode effectMode = EffectMode.None)
        {
            var maxMs = ticks * GameConstants.MsPerTick;

            // Validate the scenario really runs to the tick cap: both combatants must survive so the
            // battle does a fixed, known amount of per-tick work (required for the per-tick figures to
            // mean anything). A scenario that ended early would silently measure a different workload.
            var probe = CreateSimulator(skillCount, multiplierCount, effectsPerSkill, effectMode).Simulate(maxMs);
            Assert.False(probe.Victory, $"{label}: expected a run-to-the-cap battle, but the player won.");
            Assert.False(probe.PlayerDied, $"{label}: expected a run-to-the-cap battle, but the player died.");
            Assert.Equal(maxMs, probe.TotalMs);

            return PerformanceMeasurement.Measure(
                createInput: () => CreateSimulator(skillCount, multiplierCount, effectsPerSkill, effectMode),
                timedOperation: simulator => simulator.Simulate(maxMs),
                warmupIterations: warmup,
                sampleCount: samples,
                operationsPerSample: operationsPerSample);
        }

        private static BattleSimulator CreateSimulator(
            int skillCount, int multiplierCount, int effectsPerSkill = 0, EffectMode effectMode = EffectMode.None)
        {
            return new BattleSimulator(
                BuildBattler(skillCount, multiplierCount, effectsPerSkill, effectMode),
                BuildBattler(skillCount, multiplierCount, effectsPerSkill, effectMode),
                seed: 0);
        }

        /// <summary>
        /// Builds a combatant that deliberately never dies — its skills deal no damage (zero base, zero-weight
        /// multipliers), so neither side can be killed and the battle deterministically runs to the tick cap.
        /// (The Toughness curve never fully clamps a real hit, so immortality comes from the zero damage, not
        /// mitigation.) Skills still fire on every cooldown, so the per-tick hot path (charge,
        /// <see cref="BattleSkill.CalculateDamage"/>, stat recording, and — with churning effects — the
        /// attribute-cache invalidate/recompute cascade) is fully exercised; only the life-total
        /// bookkeeping is neutralised.
        /// </summary>
        private static Battler BuildBattler(int skillCount, int multiplierCount, int effectsPerSkill, EffectMode effectMode)
        {
            var attributes = new AttributeCollection(
            [
                Modifier(Strength, BattlerBaseStrength),
                Modifier(Endurance, BattlerBaseEndurance),
            ]);

            var skills = new List<Skill>(skillCount);
            for (var id = 0; id < skillCount; id++)
            {
                skills.Add(BuildSkill(id, multiplierCount, effectsPerSkill, effectMode));
            }

            return new Battler(attributes, skills, level: 1);
        }

        private static Skill BuildSkill(int id, int multiplierCount, int effectsPerSkill, EffectMode effectMode)
        {
            var multipliers = new List<DamageMultiplier>(multiplierCount);
            for (var i = 0; i < multiplierCount; i++)
            {
                multipliers.Add(new DamageMultiplier
                {
                    // Zero-weight so the skill deals no damage (the combatants are immortal by construction),
                    // while CalculateDamage still performs the per-multiplier read+multiply+add the cost
                    // measurement exercises.
                    Attribute = Strength,
                    Amount = 0,
                });
            }

            var effectCount = effectMode == EffectMode.None ? 0 : effectsPerSkill;
            var durationMs = effectMode == EffectMode.Churning ? ChurningEffectDurationMs : PersistentEffectDurationMs;
            var effects = new List<SkillEffect>(effectCount);
            for (var i = 0; i < effectCount; i++)
            {
                // A self-targeted additive buff on a core attribute that has derived dependents (rotated so a
                // full loadout touches every derived attribute). Persistent: spans many cooldowns, so each fire
                // STACKS another application and resets the shared per-attribute expiry before any would lapse —
                // the magnitude folds into one combined modifier (a swap) and nothing expires during the run.
                // Churning: shorter than the cooldown, so it expires and re-applies every cycle, invalidating the
                // buffed node and cascading to its derived attributes — both shapes drive the recompute path the
                // cache amortises.
                var attribute = BuffableCoreAttributes[((id * effectsPerSkill) + i) % BuffableCoreAttributes.Length];
                effects.Add(new SkillEffect
                {
                    Id = (id * 10) + i,
                    Target = ESkillEffectTarget.Self,
                    AttributeId = attribute,
                    ModifierType = EModifierType.Additive,
                    Amount = EffectBuffAmount,
                    DurationMs = durationMs,
                    ScalingAttributeId = EAttribute.Strength,
                    ScalingAmount = 0,
                });
            }

            return new Skill
            {
                Id = id,
                Name = $"Skill {id}",
                Description = string.Empty,
                DamageType = EDamageType.Physical,
                CooldownMs = SkillCooldownMs, // fires every couple of ticks, so the damage path runs heavily
                BaseDamage = 0,               // zero damage keeps both combatants immortal (runs to the cap)
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
