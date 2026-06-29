using Game.Core.Attributes;
using Game.Core.Battle;
using Game.Core.Battle.Offline;
using Game.Core.Classes;
using CoreClass = Game.Core.Classes.Class;
using Game.Core.Enemies;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Proficiencies;
using Game.Core.Skills;
using Game.Core.Zones;
using Xunit;
using static Game.Core.EAttribute;

namespace Game.Core.Tests.Battle.Offline
{
    /// <summary>
    /// Heavy unit coverage for the core offline simulation engine (#1041). Battles are made deterministic by
    /// fixing the enemy, the zone level range, and the per-battle seed, so the budget accounting and reward
    /// accumulation can be asserted exactly. A few scenarios deliberately vary the seed to exercise a mixed
    /// run of wins/losses/draws.
    /// </summary>
    public class OfflineProgressSimulatorTests
    {
        private const int CooldownMs = 5000;
        private const long TenHoursMs = 10L * 60 * 60 * 1000;

        private readonly OfflineProgressSimulator _simulator = new(new BattleFactory());

        // ── Empty / whole-skip ───────────────────────────────────────────────

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-60_000)]
        public void Simulate_NonPositiveBudget_ProducesEmptyWholeSkipResult(long awayMs)
        {
            // A non-positive away budget is the engine's "whole-skip": nothing is simulated and the result is
            // empty, which the orchestration's no-progress short-circuit relies on.
            var result = _simulator.Simulate(IdleParameters(awayMs, StrongPlayerWinScenario()));

            Assert.Empty(result.Battles);
            Assert.Equal(0, result.BattlesSimulated);
            Assert.Equal(0, result.Wins);
            Assert.Equal(0, result.Losses);
            Assert.Equal(0, result.Draws);
            Assert.Equal(0, result.TotalExp);
        }

        [Fact]
        public void Simulate_ZeroCap_ProducesEmptyResultEvenWithLongAwayTime()
        {
            // The cap clamps the budget; a zero cap leaves no budget regardless of how long the player was away.
            var parameters = With(IdleParameters(TenHoursMs, StrongPlayerWinScenario()), capMs: 0);
            var result = _simulator.Simulate(parameters);

            Assert.Empty(result.Battles);
        }

        // ── Idle loop & budget accounting ────────────────────────────────────

        [Fact]
        public void Simulate_IdleLoop_RunsUntilBudgetExhausted_AccountingDurationPlusCooldown()
        {
            var scenario = StrongPlayerWinScenario();
            // A single deterministic battle (fixed enemy + fixed seed) tells us the exact per-battle duration.
            var battleMs = SingleBattleDurationMs(scenario);
            var stepMs = battleMs + CooldownMs;
            var awayMs = (stepMs * 4) + 1; // 4 full steps plus a sliver → a 5th battle starts

            var result = _simulator.Simulate(IdleParameters(awayMs, scenario));

            // Every battle is identical, so each consumes the same duration.
            Assert.All(result.Battles, battle => Assert.Equal(battleMs, battle.Result.TotalMs));

            // The loop keeps fighting while any budget remains, consuming duration + cooldown per battle, and
            // stops once the budget is exhausted: the run is the fewest battles whose total cost covers the
            // budget (the last battle overshoots by at most one step).
            var count = result.BattlesSimulated;
            Assert.True((count - 1L) * stepMs < awayMs, "The run stopped before the budget was exhausted.");
            Assert.True(count * stepMs >= awayMs, "The run continued past an already-exhausted budget.");
            Assert.Equal(5, count);
        }

        [Fact]
        public void Simulate_AwayBudgetBelowOneStep_RunsExactlyOneBattle()
        {
            var scenario = StrongPlayerWinScenario();

            // Any positive budget fights at least one battle; the battle (plus cooldown) then exhausts it.
            var result = _simulator.Simulate(IdleParameters(awayMs: 1, scenario));

            Assert.Single(result.Battles);
        }

        [Fact]
        public void Simulate_IdleLoop_RollsEncounterLevelWithinZoneRange()
        {
            // Resolve the enemy at whatever level the idle loop rolls, recording the levels seen. The engine
            // must drive the random idle encounter (BattleFactory.CreateBattleEnemy) within the zone's range.
            var levels = new HashSet<int>();
            var scenario = new Scenario
            {
                Zone = MakeZone(levelMin: 3, levelMax: 6),
                // A strong player wins each battle quickly, so the run packs in many fast battles.
                Snapshot = PlayerSnapshot(strength: 100, endurance: 100),
                ResolveEnemy = level =>
                {
                    levels.Add(level);
                    return WeakEnemy(level);
                },
            };

            // Plenty of battles to exercise the whole range.
            _simulator.Simulate(IdleParameters(TenHoursMs, scenario, capMs: TenHoursMs));

            Assert.All(levels, level => Assert.InRange(level, 3, 6));
            // The range has four levels; over thousands of battles every one should appear.
            Assert.Equal(4, levels.Count);
        }

        // ── Cap vs budget boundary ───────────────────────────────────────────

        [Fact]
        public void Simulate_AwayExceedsCap_ClampsToCap()
        {
            var scenario = StrongPlayerWinScenario();
            var battleMs = SingleBattleDurationMs(scenario);
            var stepMs = battleMs + CooldownMs;
            var capMs = stepMs * 3;

            // Away far exceeds the cap, so the run is bounded by the cap, not the away time.
            var clamped = _simulator.Simulate(With(IdleParameters(TenHoursMs, scenario), capMs: capMs));
            // A run with away == cap is the reference: clamping must reproduce it exactly.
            var atCap = _simulator.Simulate(With(IdleParameters(capMs, scenario), capMs: capMs));

            Assert.Equal(3, clamped.BattlesSimulated);
            Assert.Equal(atCap.BattlesSimulated, clamped.BattlesSimulated);
        }

        // ── Boss loop ────────────────────────────────────────────────────────

        [Fact]
        public void Simulate_BossLoop_BuildsDeterministicBossEachBattle()
        {
            // Boss mode must fight the zone's dedicated boss at its fixed level with the full authored loadout,
            // every battle — not a random idle encounter.
            var resolvedLevels = new List<int>();
            var scenario = new Scenario
            {
                Zone = MakeZone(levelMin: 1, levelMax: 1, bossEnemyId: 7, bossLevel: 12),
                ResolveEnemy = level =>
                {
                    resolvedLevels.Add(level);
                    return WeakEnemy(level, skillCount: 3);
                },
            };

            var result = _simulator.Simulate(BossParameters(TenHoursMs, scenario, capMs: TenHoursMs));

            Assert.NotEmpty(result.Battles);
            Assert.All(resolvedLevels, level => Assert.Equal(12, level)); // always the fixed boss level
            // The full authored loadout (no 4-skill cap) is brought into each boss battle.
            Assert.All(result.Battles, battle => Assert.Equal(3, battle.Enemy.BattleSkills.Count));
            Assert.Equal(OfflineLoopMode.Boss, result.Mode);
            Assert.True(result.IsBossBattle);
        }

        [Fact]
        public void Simulate_BossLoop_ContinuesThroughLosses()
        {
            // A player guaranteed to die must not stop the loop — offline boss farming keeps going through
            // losses (decision 3), unlike the present-player loop that drops to idle.
            var result = _simulator.Simulate(BossParameters(ManyStepsBudget(), AlwaysLoseBossScenario()));

            Assert.True(result.BattlesSimulated > 1, "The boss loop stopped after a single loss.");
            Assert.Equal(result.BattlesSimulated, result.Losses);
            Assert.Equal(0, result.Wins);
            Assert.Equal(0, result.Draws);
        }

        [Fact]
        public void Simulate_BossLoop_VariedSeeds_ProducesMixedOutcomesMatchingDirectSimulation()
        {
            // A balanced boss fight whose outcome turns on the player's crit rolls: a fresh seed each battle
            // varies the result. Drive a fixed seed sequence so the run is deterministic, and verify each
            // battle matches an independent direct simulation of the same seed (proving fresh-seed-per-battle
            // and that every outcome type is carried through).
            var scenario = CoinFlipBossScenario();
            // A generous seed pool (larger than any battle count this budget can produce) keyed by index, so
            // every battle draws a distinct, known seed and the source never overruns.
            var seeds = Enumerable.Range(0, 1024).Select(i => (uint)i).ToArray();
            var seedIndex = 0;
            var parameters = BossParameters(ManyStepsBudget(15), scenario) with
            {
                SeedSource = () => seeds[seedIndex++],
            };

            var result = _simulator.Simulate(parameters);

            Assert.True(result.BattlesSimulated > 1);
            Assert.Equal(result.BattlesSimulated, result.Wins + result.Losses + result.Draws);

            // The run is a genuine mix, not a single repeated outcome.
            var distinctOutcomeKinds = new[] { result.Wins, result.Losses, result.Draws }.Count(c => c > 0);
            Assert.True(distinctOutcomeKinds >= 2,
                $"Expected a mix of outcomes but got {result.Wins} wins / {result.Losses} losses / {result.Draws} draws.");

            // Each simulated battle reproduces a direct BattleSimulator run of the same seed, in order —
            // proving a fresh seed is consumed per battle and the loop carries every outcome type through.
            for (var i = 0; i < result.BattlesSimulated; i++)
            {
                var expected = DirectBossSimulation(scenario, seeds[i]);
                var actual = result.Battles[i].Result;
                Assert.Equal(expected.Victory, actual.Victory);
                Assert.Equal(expected.PlayerDied, actual.PlayerDied);
                Assert.Equal(expected.TotalMs, actual.TotalMs);
            }
        }

        // ── All-draw (zero rewards) ──────────────────────────────────────────

        [Fact]
        public void Simulate_AllDrawZone_ProducesBattlesButZeroRewards()
        {
            // Neither side can damage the other, so every battle runs to the cap as a draw — the most work for
            // no reward. The result has battles but no wins, no exp, no kills (the no-progress short-circuit).
            var result = _simulator.Simulate(IdleParameters(ManyStepsBudget(), StalemateScenario()));

            Assert.True(result.BattlesSimulated > 1);
            Assert.Equal(result.BattlesSimulated, result.Draws);
            Assert.Equal(0, result.Wins);
            Assert.Equal(0, result.Losses);
            Assert.Equal(0, result.TotalExp);
            Assert.All(result.Battles, battle =>
            {
                Assert.False(battle.Result.Victory);
                Assert.False(battle.Result.PlayerDied);
                Assert.Equal(0, battle.ExpReward);
            });
        }

        [Fact]
        public void Simulate_AllDrawBoss_ProducesZeroRewards()
        {
            var scenario = StalemateScenario(bossEnemyId: 7, bossLevel: 3);

            var result = _simulator.Simulate(BossParameters(ManyStepsBudget(), scenario));

            Assert.True(result.BattlesSimulated > 1);
            Assert.Equal(result.BattlesSimulated, result.Draws);
            Assert.Equal(0, result.TotalExp);
        }

        // ── Stalemate cutoff (CPU-waste guard) ───────────────────────────────

        [Fact]
        public void Simulate_StalemateCutoff_StopsAfterOpeningAllDrawBatch()
        {
            // An all-draw stalemate over a budget that would otherwise run many battles: with the cutoff set,
            // the loop stops once the opening batch has been nothing but draws, instead of burning the whole
            // budget on maximum-duration draws for no reward.
            var parameters = IdleParameters(ManyStepsBudget(), StalemateScenario()) with
            {
                StalemateCutoffBattles = 5,
            };

            var result = _simulator.Simulate(parameters);

            Assert.Equal(5, result.BattlesSimulated);
            Assert.Equal(5, result.Draws);
            Assert.Equal(0, result.Wins);
            Assert.Equal(0, result.Losses);
        }

        [Fact]
        public void Simulate_StalemateCutoff_DoesNotFireWhenTheOpeningBatchProducesWins()
        {
            // A winning run makes progress in the opening batch, so the cutoff never fires and the loop runs
            // the whole budget exactly as it would without the guard.
            var scenario = StrongPlayerWinScenario();
            var withCutoff = IdleParameters(ManyStepsBudget(), scenario) with { StalemateCutoffBattles = 5 };
            var withoutCutoff = IdleParameters(ManyStepsBudget(), scenario);

            var guarded = _simulator.Simulate(withCutoff);
            var unguarded = _simulator.Simulate(withoutCutoff);

            Assert.True(guarded.Wins > 5, "The guard cut a winning run short.");
            Assert.Equal(unguarded.BattlesSimulated, guarded.BattlesSimulated);
        }

        [Fact]
        public void Simulate_StalemateCutoff_DoesNotFireWhenTheOpeningBatchProducesLosses()
        {
            // A losing run is also progress (and cheap — quick battles), so the cutoff must not fire on it;
            // only a pure-draw opening batch is the stalemate the guard targets.
            var parameters = BossParameters(ManyStepsBudget(), AlwaysLoseBossScenario()) with
            {
                StalemateCutoffBattles = 5,
            };

            var result = _simulator.Simulate(parameters);

            Assert.True(result.BattlesSimulated > 5, "The guard cut a losing run short.");
            Assert.Equal(result.BattlesSimulated, result.Losses);
        }

        // ── Reward accumulation ──────────────────────────────────────────────

        [Fact]
        public void Simulate_Wins_TotalExpEqualsSumOfPerVictoryRewards()
        {
            var scenario = StrongPlayerWinScenario();

            var result = _simulator.Simulate(IdleParameters(ManyStepsBudget(), scenario));

            Assert.True(result.Wins > 1);
            Assert.Equal(result.BattlesSimulated, result.Wins); // the strong player wins every battle
            var summed = result.Battles.Where(b => b.Result.Victory).Sum(b => (long)b.ExpReward);
            Assert.Equal(summed, result.TotalExp);
        }

        [Fact]
        public void Simulate_PerVictoryExp_MatchesDefeatRewardsFromSnapshot()
        {
            var scenario = StrongPlayerWinScenario();

            var result = _simulator.Simulate(IdleParameters(ManyStepsBudget(), scenario));

            // Each victory's exp is exactly what DefeatRewards computes from the same snapshot/enemy — the
            // reward is consistent with the battle it was earned in.
            var playerModifiers = scenario.Snapshot
                .GetModifiers(scenario.ResolveItem, scenario.ResolveMod)
                .ToList();
            Assert.All(result.Battles, battle =>
            {
                var expected = new DefeatRewards(playerModifiers, battle.Enemy).ExpReward;
                Assert.Equal(expected, battle.ExpReward);
            });
        }

        [Fact]
        public void Simulate_CarriesEachBattlesStatsThrough_ForStatisticsConsolidation()
        {
            // The whole BattleResult — including its BattleStats — rides on each outcome so the orchestration
            // layer (#1042) can feed every battle through the shared per-battle statistics-recording path. Pin
            // that the stats are the real per-battle combat figures, not defaults.
            var result = _simulator.Simulate(IdleParameters(ManyStepsBudget(), StrongPlayerWinScenario()));

            Assert.NotEmpty(result.Battles);
            Assert.All(result.Battles, battle =>
            {
                Assert.True(battle.Result.Stats.PlayerDamageDealt > 0);
                Assert.True(battle.Result.Stats.PlayerSkillsUsed > 0);
                Assert.NotEmpty(battle.Result.Stats.SkillStats);
            });
        }

        // ── Cancellation ─────────────────────────────────────────────────────

        [Fact]
        public void Simulate_CancellationRequested_Throws()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                _simulator.Simulate(IdleParameters(TenHoursMs, StrongPlayerWinScenario()), cts.Token));
        }

        // ── Result metadata ──────────────────────────────────────────────────

        [Fact]
        public void Simulate_ResultCarriesModeAndZone()
        {
            var scenario = StrongPlayerWinScenario();
            scenario.Zone = MakeZone(levelMin: 1, levelMax: 1, id: 42);

            var result = _simulator.Simulate(IdleParameters(ManyStepsBudget(), scenario));

            Assert.Equal(OfflineLoopMode.Idle, result.Mode);
            Assert.False(result.IsBossBattle);
            Assert.Equal(42, result.ZoneId);
        }

        // ── Class locked base ────────────────────────────────────────────────

        [Fact]
        public void Simulate_AppliesClassLockedBase_FromTheFrozenSnapshotLevel()
        {
            // The player's free pool is empty, so the class locked base is its entire attribute spread. With a
            // strong fingerprint the player wins idle battles it wins none of without one — proving the offline
            // path composes the locked base. The base is derived from the snapshot's frozen level, so (like
            // every other captured input) it is stationary across the away window.
            var zone = MakeZone(levelMin: 1, levelMax: 1);
            var snapshot = ClassPlayerSnapshot(level: 1, classId: 2);

            OfflineProgressResult Run(params AttributeDistribution[] distributions)
            {
                var scenario = new Scenario { Zone = zone, Snapshot = snapshot, ResolveEnemy = level => WeakEnemy(level) };
                return _simulator.Simulate(IdleParameters(ManyStepsBudget(), scenario) with
                {
                    ResolveClass = ClassResolver(2, distributions),
                });
            }

            var withoutFingerprint = Run();
            var withFingerprint = Run(Distribution(Strength, 100m), Distribution(Endurance, 100m));

            Assert.Equal(0, withoutFingerprint.Wins);
            Assert.True(withFingerprint.Wins > 0);
        }

        [Fact]
        public void Simulate_AppliesClassSignaturePassive_FromTheFrozenSnapshot()
        {
            // The signature passive (#1126 area E) feeds the same attribute pipeline as the locked base, so a
            // strong flat-core passive lets the player win idle battles it wins none of without one — proving
            // the offline path composes the passive, not just the locked base. The class carries no locked-base
            // distribution, so the wins are attributable to the passive alone.
            var zone = MakeZone(levelMin: 1, levelMax: 1);
            var snapshot = ClassPlayerSnapshot(level: 1, classId: 2);

            OfflineProgressResult Run(ClassSignaturePassive passive)
            {
                var scenario = new Scenario { Zone = zone, Snapshot = snapshot, ResolveEnemy = level => WeakEnemy(level) };
                return _simulator.Simulate(IdleParameters(ManyStepsBudget(), scenario) with
                {
                    ResolveClass = ClassResolver(2, passive),
                });
            }

            var withoutPassive = Run(NoopPassive());
            var withPassive = Run(FlatPassive(Strength, 100m));

            Assert.Equal(0, withoutPassive.Wins);
            Assert.True(withPassive.Wins > 0);
        }

        // ── Scenario plumbing ────────────────────────────────────────────────

        private const int EnemyId = 1;

        /// <summary>A self-contained battle scenario: the zone, the player snapshot, and the resolvers.</summary>
        private sealed class Scenario
        {
            public required Zone Zone { get; set; }
            public BattleSnapshot Snapshot { get; init; } = EmptySnapshot();
            public required Func<int, Enemy> ResolveEnemy { get; init; }
            public Func<int, Item> ResolveItem { get; init; } = ThrowItem;
            public Func<int, ItemMod> ResolveMod { get; init; } = ThrowMod;
            public Func<int, Skill> ResolveSkill { get; init; } = id => PlayerAttackSkill();
        }

        private static OfflineSimulationParameters IdleParameters(long awayMs, Scenario scenario, long capMs = TenHoursMs) =>
            new()
            {
                Snapshot = scenario.Snapshot,
                Mode = OfflineLoopMode.Idle,
                Zone = scenario.Zone,
                AwayBudgetMs = awayMs,
                CapMs = capMs,
                CooldownMs = CooldownMs,
                ResolveEnemy = scenario.ResolveEnemy,
                ResolveItem = scenario.ResolveItem,
                ResolveMod = scenario.ResolveMod,
                ResolveSkill = scenario.ResolveSkill,
                ResolveProficiency = ThrowProficiency,
                ResolveClass = ThrowClass,
                SeedSource = () => 0,
            };

        private static OfflineSimulationParameters BossParameters(long awayMs, Scenario scenario, long capMs = TenHoursMs) =>
            IdleParameters(awayMs, scenario, capMs) with { Mode = OfflineLoopMode.Boss };

        private static OfflineSimulationParameters With(OfflineSimulationParameters parameters, long capMs) =>
            parameters with { CapMs = capMs };

        // A budget comfortably larger than several battle steps, so a run produces many battles.
        private static long ManyStepsBudget(int steps = 20) =>
            steps * (GameConstants.DefaultMaxBattleMs + CooldownMs);

        /// <summary>Runs a single deterministic battle (fixed enemy + fixed seed) to read its exact duration.</summary>
        private static int SingleBattleDurationMs(Scenario scenario)
        {
            var enemy = new BattleFactory().CreateBattleEnemy(scenario.Zone, scenario.ResolveEnemy);
            return DirectSimulation(scenario, enemy, seed: 0).TotalMs;
        }

        private static BattleResult DirectBossSimulation(Scenario scenario, uint seed)
        {
            var enemy = new BattleFactory().CreateBossEnemy(scenario.Zone, scenario.ResolveEnemy);
            return DirectSimulation(scenario, enemy, seed);
        }

        private static BattleResult DirectSimulation(Scenario scenario, Enemy enemy, uint seed)
        {
            var playerBattler = scenario.Snapshot.ToBattler(scenario.ResolveItem, scenario.ResolveMod, scenario.ResolveSkill);
            var enemyBattler = new Battler(
                new AttributeCollection(enemy.GetAttributeModifiers()), enemy.BattleSkills, enemy.Level);
            return new BattleSimulator(playerBattler, enemyBattler, seed).Simulate();
        }

        // ── Scenario presets ─────────────────────────────────────────────────

        private static Scenario StrongPlayerWinScenario() => new()
        {
            Zone = MakeZone(levelMin: 1, levelMax: 1),
            Snapshot = PlayerSnapshot(strength: 100, endurance: 100),
            ResolveEnemy = level => WeakEnemy(level),
        };

        private static Scenario AlwaysLoseBossScenario() => new()
        {
            Zone = MakeZone(levelMin: 1, levelMax: 1, bossEnemyId: 7, bossLevel: 1),
            Snapshot = PlayerSnapshot(strength: 1, endurance: 1),
            ResolveEnemy = level => StrongEnemy(level),
        };

        private static Scenario StalemateScenario(int? bossEnemyId = null, int bossLevel = 1) => new()
        {
            // Both sides have huge Endurance, which gives both an enormous MaxHealth AND an enormous Toughness —
            // so the trickle of post-mitigation damage cannot kill within the cap and every battle is a draw.
            Zone = MakeZone(levelMin: 1, levelMax: 1, bossEnemyId: bossEnemyId, bossLevel: bossLevel),
            Snapshot = PlayerSnapshot(strength: 1, endurance: 1000),
            ResolveEnemy = level => MakeEnemy(level, strength: 1, endurance: 1000, skillCount: 1),
        };

        /// <summary>
        /// A boss fight balanced so the player's crit rolls decide it. The boss carries an injected Toughness
        /// (so its MaxHealth stays low) that heavily mitigates every player hit; a crit multiplies the raw
        /// damage before mitigation, so it deals 1.75× a normal hit. The player's long skill cooldown limits it
        /// to ~4 fires across the cap, and the boss's MaxHealth is tuned so it dies only once at least two of
        /// those fires crit — so whether enough crit, varying with each battle's fresh seed, flips the outcome
        /// between a win and a draw. The boss's chip damage never out-races the player's MaxHealth, so the
        /// player never dies (the loop's continue-through-losses behaviour is pinned separately by
        /// <see cref="AlwaysLoseBossScenario"/>).
        /// </summary>
        private static Scenario CoinFlipBossScenario() => new()
        {
            Zone = MakeZone(levelMin: 1, levelMax: 1, bossEnemyId: 7, bossLevel: 1),
            // Dexterity/Luck drive a ~30% crit chance and a 1.75x crit multiplier.
            Snapshot = PlayerSnapshot(strength: 50, endurance: 10, dexterity: 100, luck: 100),
            // A long cooldown → only ~4 fires across the 120s cap, so crit count (and thus the outcome)
            // swings battle to battle.
            ResolveSkill = _ => SlowHeavySkill(),
            ResolveEnemy = level => CoinFlipBoss(level),
        };

        // ── Builders ─────────────────────────────────────────────────────────

        private static Zone MakeZone(int levelMin, int levelMax, int? bossEnemyId = null, int bossLevel = 1, int id = 1) => new()
        {
            Id = id,
            LevelMin = levelMin,
            LevelMax = levelMax,
            BossEnemyId = bossEnemyId,
            BossLevel = bossLevel,
            UnlockChallengeId = null,
        };

        private static BattleSnapshot EmptySnapshot() => new()
        {
            Level = 1,
            StatAllocations = [],
            EquippedItems = [],
            SkillIds = [0],
        };

        private static BattleSnapshot PlayerSnapshot(
            double strength = 0, double endurance = 0, double dexterity = 0, double luck = 0)
        {
            return new()
            {
                Level = 1,
                StatAllocations =
                [
                    new StatAllocation { Attribute = Strength, Amount = strength },
                    new StatAllocation { Attribute = Endurance, Amount = endurance },
                    new StatAllocation { Attribute = Dexterity, Amount = dexterity },
                    new StatAllocation { Attribute = Luck, Amount = luck },
                ],
                EquippedItems = [],
                SkillIds = [0],
            };
        }

        private static BattleSnapshot ClassPlayerSnapshot(int level, int classId) => new()
        {
            Level = level,
            ClassId = classId,
            StatAllocations = [],
            EquippedItems = [],
            SkillIds = [0],
        };

        private static Func<int, CoreClass> ClassResolver(int classId, params AttributeDistribution[] distributions) =>
            ClassResolver(classId, NoopPassive(), distributions);

        private static Func<int, CoreClass> ClassResolver(
            int classId, ClassSignaturePassive passive, params AttributeDistribution[] distributions) =>
            id => id == classId
                ? MakeClass(classId, passive, distributions)
                : throw new InvalidOperationException($"Unexpected class resolve for {id}");

        private static CoreClass MakeClass(
            int id, ClassSignaturePassive passive, params AttributeDistribution[] distributions) => new()
            {
                Id = id,
                Name = $"Class {id}",
                StarterSkillIds = [],
                StarterEquipment = [],
                AttributeDistributions = distributions,
                SignaturePassive = passive,
            };

        private static ClassSignaturePassive NoopPassive() => new()
        {
            Attribute = Strength,
            Amount = 0m,
            ScalingAttribute = null,
            ScalingAmount = 0m,
            ModifierType = EModifierType.Additive,
        };

        private static ClassSignaturePassive FlatPassive(EAttribute attribute, decimal amount) => new()
        {
            Attribute = attribute,
            Amount = amount,
            ScalingAttribute = null,
            ScalingAmount = 0m,
            ModifierType = EModifierType.Additive,
        };

        private static AttributeDistribution Distribution(EAttribute attribute, decimal baseAmount, decimal amountPerLevel = 0m) =>
            new() { AttributeId = attribute, BaseAmount = baseAmount, AmountPerLevel = amountPerLevel };

        private static Enemy WeakEnemy(int level, int skillCount = 1) =>
            MakeEnemy(level, strength: 5, endurance: 5, skillCount: skillCount);

        private static Enemy StrongEnemy(int level, int skillCount = 1) =>
            MakeEnemy(level, strength: 200, endurance: 200, skillCount: skillCount);

        private static Enemy MakeEnemy(int level, double strength, double endurance, int skillCount)
        {
            var skills = Enumerable.Range(0, skillCount).Select(EnemyAttackSkill).ToList();
            return new Enemy
            {
                Id = EnemyId,
                Name = "Test Enemy",
                IsBoss = false,
                Level = level,
                AttributeDistributions =
                [
                    new AttributeDistribution { AttributeId = Strength, BaseAmount = (decimal)strength, AmountPerLevel = 0 },
                    new AttributeDistribution { AttributeId = Endurance, BaseAmount = (decimal)endurance, AmountPerLevel = 0 },
                ],
                AvailableSkills = skills,
            };
        }

        private static Skill PlayerAttackSkill() => new()
        {
            Id = 0,
            Name = "Attack",
            Description = string.Empty,
            Rarity = ERarity.Common,
            DamageType = EDamageType.Physical,
            CooldownMs = 1000,
            BaseDamage = 10,
            DamageMultipliers = [new DamageMultiplier { Attribute = Strength, Amount = 1.0 }],
            Effects = [],
        };

        // A high-cooldown variant of the player's attack: it fires only a few times across the battle cap,
        // so the count of crits among those fires (and thus the outcome) swings with each battle's seed.
        private static Skill SlowHeavySkill() => new()
        {
            Id = 0,
            Name = "Heavy Strike",
            Description = string.Empty,
            Rarity = ERarity.Common,
            DamageType = EDamageType.Physical,
            CooldownMs = 30_000,
            BaseDamage = 10,
            DamageMultipliers = [new DamageMultiplier { Attribute = Strength, Amount = 1.0 }],
            Effects = [],
        };

        // The coin-flip boss: an injected Toughness 80 heavily mitigates each hit while Strength 2 keeps
        // MaxHealth low (50 + 5·2 = 60). Against the level-1 player (K·level = 20) the curve reduces by
        // 80/(80+20) = 0.8, so a normal 60-damage hit deals 60×0.2 = 12 and a 1.75× crit (105) deals 21.
        // Across 4 fires the boss takes 48 + 9·crits, so it dies (≥60) only with at least two crits — the
        // crit count flips the outcome between a win and a draw.
        private static Enemy CoinFlipBoss(int level) => new()
        {
            Id = EnemyId,
            Name = "Coin-Flip Boss",
            IsBoss = true,
            Level = level,
            AttributeDistributions =
            [
                new AttributeDistribution { AttributeId = Strength, BaseAmount = 2, AmountPerLevel = 0 },
                new AttributeDistribution { AttributeId = Toughness, BaseAmount = 80, AmountPerLevel = 0 },
            ],
            AvailableSkills = [EnemyAttackSkill(0)],
        };

        private static Skill EnemyAttackSkill(int id) => new()
        {
            Id = id,
            Name = $"Scratch {id}",
            Description = string.Empty,
            Rarity = ERarity.Common,
            DamageType = EDamageType.Physical,
            CooldownMs = 1500,
            BaseDamage = 5,
            DamageMultipliers = [],
            Effects = [],
        };

        private static readonly Func<int, Item> ThrowItem =
            id => throw new InvalidOperationException($"Unexpected item resolve for {id}");
        private static readonly Func<int, ItemMod> ThrowMod =
            id => throw new InvalidOperationException($"Unexpected mod resolve for {id}");
        // These scenarios capture no proficiency levels, so the snapshot never resolves a proficiency.
        private static readonly Func<int, Proficiency> ThrowProficiency =
            id => throw new InvalidOperationException($"Unexpected proficiency resolve for {id}");
        // These scenarios capture no class (the snapshot's ClassId is null), so it never resolves a class.
        private static readonly Func<int, CoreClass> ThrowClass =
            id => throw new InvalidOperationException($"Unexpected class resolve for {id}");
    }
}
