using System.Diagnostics.CodeAnalysis;
using Game.Core.Attributes;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Players;

namespace Game.Core.Progress
{
    public class PlayerProgress
    {
        private readonly Dictionary<(EStatisticType Type, int? EntityId), PlayerStatistic> _statistics;
        private readonly Dictionary<int, PlayerChallenge> _challenges;
        private readonly Dictionary<int, PlayerProficiency> _proficiencies;

        // Rows mutated since this aggregate was loaded. The write-behind save enqueues only these (as
        // absolute values), and the cache — the source of truth — advances the same way: it is a Redis hash
        // keyed by row identity, so only the dirty rows are written rather than the whole, ever-growing set.
        private readonly HashSet<(EStatisticType Type, int? EntityId)> _dirtyStatistics = [];
        private readonly HashSet<int> _dirtyChallenges = [];
        private readonly HashSet<int> _dirtyProficiencies = [];

        public Player Player { get; }
        public IEnumerable<PlayerStatistic> Statistics => _statistics.Values;
        public IEnumerable<PlayerChallenge> ChallengeProgress => _challenges.Values;
        public IEnumerable<PlayerProficiency> Proficiencies => _proficiencies.Values;

        /// <summary>The statistics changed since load — what the write-behind save persists.</summary>
        public IEnumerable<PlayerStatistic> DirtyStatistics => _dirtyStatistics.Select(key => _statistics[key]);

        /// <summary>The challenge-progress rows changed since load — what the write-behind save persists.</summary>
        public IEnumerable<PlayerChallenge> DirtyChallenges => _dirtyChallenges.Select(id => _challenges[id]);

        /// <summary>The proficiency-progress rows changed since load — what the write-behind save persists.</summary>
        public IEnumerable<PlayerProficiency> DirtyProficiencies => _dirtyProficiencies.Select(id => _proficiencies[id]);

        public PlayerProgress(
            Player player,
            IEnumerable<PlayerStatistic> statistics,
            IEnumerable<PlayerChallenge> challengeProgress,
            IEnumerable<PlayerProficiency> proficiencies)
        {
            Player = player;
            _statistics = statistics.ToDictionary(s => (s.Type, s.EntityId));
            _challenges = challengeProgress.ToDictionary(c => c.Challenge.Id);
            _proficiencies = proficiencies.ToDictionary(p => p.ProficiencyId);
        }

        /// <summary>
        /// Records the outcome of a completed battle and returns the (statistic type, entity) keys it
        /// touched. On a freshly loaded aggregate (one is built per battle) those keys are exactly this
        /// battle's mutated statistics, so the caller can scope challenge evaluation to the challenges that
        /// track them (see <see cref="ChallengeIndex.RelevantTo"/>) instead of re-scanning the whole catalog.
        /// </summary>
        public IReadOnlyCollection<(EStatisticType Type, int? EntityId)> RecordBattleCompleted(
            Enemy enemy, bool victory, bool playerDied, int totalMs, BattleStats stats, bool isBossBattle, int zoneId)
        {
            Record(EStatisticType.DamageDealt, null, (decimal)Math.Round(stats.PlayerDamageDealt, 3));
            Record(EStatisticType.HighestSingleAttackDamage, null, (decimal)Math.Round(stats.HighestPlayerAttack, 3));
            Record(EStatisticType.DamageTaken, null, (decimal)Math.Round(stats.PlayerDamageTaken, 3));
            Record(EStatisticType.DamageHealed, null, (decimal)Math.Round(stats.PlayerDamageHealed, 3));
            Record(EStatisticType.EnemiesEncountered, null, 1);
            Record(EStatisticType.EnemiesEncountered, enemy.Id, 1);

            // Player-only crit/dodge/parry tallies — recorded globally only (no per-skill/per-enemy breakdown),
            // alongside the damage statistics they mirror.
            Record(EStatisticType.CriticalHits, null, stats.CriticalHits);
            Record(EStatisticType.CriticalDamageDealt, null, (decimal)Math.Round(stats.CriticalDamageDealt, 3));
            Record(EStatisticType.AttacksDodged, null, stats.AttacksDodged);
            Record(EStatisticType.DamageDodged, null, (decimal)Math.Round(stats.DamageDodged, 3));
            Record(EStatisticType.AttacksParried, null, stats.AttacksParried);
            Record(EStatisticType.DamageParried, null, (decimal)Math.Round(stats.DamageParried, 3));
            Record(EStatisticType.CounterDamageDealt, null, (decimal)Math.Round(stats.PlayerCounterDamageDealt, 3));

            if (victory)
            {
                Record(EStatisticType.BattlesWon, null, 1);
                Record(EStatisticType.BattlesWon, enemy.Id, 1);
                Record(EStatisticType.FastestVictory, null, totalMs / 1000m);
                Record(EStatisticType.FastestVictory, enemy.Id, totalMs / 1000m);
                Record(EStatisticType.EnemiesKilled, null, 1);
                Record(EStatisticType.EnemiesKilled, enemy.Id, 1);
                RecordKillByDamageType(stats);

                // A dedicated-boss victory both farms the boss and (on the first clear) clears its zone. The
                // boss-ness comes from the explicit challenge marker (threaded from PlayerState), not the
                // enemy's IsBoss flag, so only the "Challenge Boss" path counts — never a boss that happens to
                // roll out of a random spawn table.
                if (isBossBattle)
                {
                    // BossesDefeated is the farm counter: it increments on every dedicated-boss victory,
                    // tracked globally and per-boss so challenges can target either "defeat any boss" or a
                    // specific boss repeatedly.
                    Record(EStatisticType.BossesDefeated, null, 1);
                    Record(EStatisticType.BossesDefeated, enemy.Id, 1);

                    // ZonesCleared counts distinct zones ever cleared, not boss-victory events. The per-zone
                    // entry is a binary "cleared" flag whose absence (not a magic 0) is the "never cleared"
                    // state, so the global counter only bumps on a zone's first clear and re-farming a boss
                    // never re-counts the zone — keying off row presence per the documented invariant.
                    if (!TryGetStatisticValue(EStatisticType.ZonesCleared, zoneId, out _))
                    {
                        Record(EStatisticType.ZonesCleared, null, 1);
                        Record(EStatisticType.ZonesCleared, zoneId, 1);
                    }
                }
            }
            else if (playerDied)
            {
                // The player died, so the battle was genuinely lost.
                Record(EStatisticType.BattlesLost, null, 1);
                Record(EStatisticType.BattlesLost, enemy.Id, 1);
            }
            else
            {
                // Neither combatant died — the battle was abandoned mid-fight (e.g. the player retreated
                // from a boss or switched zones). Tracked separately from a loss so the two are distinct
                // numbers (#202).
                Record(EStatisticType.BattlesAbandoned, null, 1);
                Record(EStatisticType.BattlesAbandoned, enemy.Id, 1);
            }

            if (playerDied)
            {
                Record(EStatisticType.PlayerDeaths, null, 1);
            }

            Record(EStatisticType.TotalBattleTime, null, totalMs / 1000m);
            Record(EStatisticType.SkillsUsed, null, stats.PlayerSkillsUsed);

            foreach (var (skillId, skillStats) in stats.SkillStats)
            {
                Record(EStatisticType.SkillsUsed, skillId, skillStats.Uses);
                Record(EStatisticType.DamageDealt, skillId, (decimal)Math.Round(skillStats.TotalDamage, 3));
                Record(EStatisticType.HighestSingleAttackDamage, skillId, (decimal)Math.Round(skillStats.HighestSingleAttack, 3));
            }

            // The mutated rows are exactly the statistics this battle touched (the aggregate was freshly
            // loaded), so they double as the relevance scope for challenge evaluation.
            return [.. _dirtyStatistics];
        }

        public List<CompletedChallenge> EvaluateChallenges(IEnumerable<Challenge> challenges)
        {
            var completed = new List<CompletedChallenge>();

            foreach (var challenge in challenges)
            {
                if (_challenges.TryGetValue(challenge.Id, out var playerChallenge) && playerChallenge.Completed)
                {
                    continue;
                }

                // A retired challenge is out of circulation: a player who has not already completed it (the
                // guard above) can no longer progress toward or complete it. Retirement never revokes an
                // existing completion's reward — it only stops new completions, matching the retired-enemy
                // precedent (a retired enemy drops out of random spawns).
                if (challenge.RetiredAt is not null)
                {
                    continue;
                }

                // Evaluate a newly-relevant challenge against a working row without committing it yet: a
                // challenge that becomes relevant but gains no progress this battle must not persist an
                // information-free (zero-progress, incomplete) row, which would bloat the write-behind and
                // the cache snapshot and partially undo the RelevantTo reverse-index scoping.
                var isNew = playerChallenge is null;
                playerChallenge ??= new PlayerChallenge(challenge, progress: 0, completed: false);

                var beforeProgress = playerChallenge.Progress;
                var beforeCompleted = playerChallenge.Completed;

                challenge.UpdateChallengeProgress(playerChallenge, this);

                if (playerChallenge.Progress != beforeProgress || playerChallenge.Completed != beforeCompleted)
                {
                    // Commit a freshly-created row only once it actually carries information.
                    if (isNew)
                    {
                        _challenges[challenge.Id] = playerChallenge;
                    }

                    _dirtyChallenges.Add(challenge.Id);
                }

                if (playerChallenge.Completed)
                {
                    completed.Add(new CompletedChallenge
                    {
                        ChallengeId = challenge.Id,
                        RewardItemId = challenge.RewardItemId,
                        RewardItemModId = challenge.RewardItemModId,
                    });
                }
            }

            return completed;
        }

        /// <summary>
        /// The ids of the challenges the player has completed — the in-aggregate equivalent of
        /// <c>IPlayerProgressRepository.GetCompletedChallengeIds</c>, for callers that already hold a loaded
        /// aggregate (e.g. the offline pass) so they need not re-read the progress cache key just to gate on it.
        /// </summary>
        public HashSet<int> CompletedChallengeIds() =>
            [.. _challenges.Values.Where(c => c.Completed).Select(c => c.Challenge.Id)];

        /// <summary>
        /// Looks up a recorded statistic value, collapsing "no row" and "recorded 0" to the same <c>0m</c>.
        /// Production code must always distinguish the two (see <see cref="TryGetStatisticValue"/>'s remarks) and
        /// so never calls this; it is kept as a deliberate test-only convenience — most test assertions only
        /// care about the value, not row presence — rather than removed.
        /// </summary>
        public decimal GetStatisticValue(EStatisticType type, int? entityId)
        {
            TryGetStatisticValue(type, entityId, out var value);
            return value;
        }

        /// <summary>
        /// Looks up a recorded statistic value, reporting whether a row actually exists.
        /// <para>
        /// The canonical "no data yet" representation is the <b>absence of a row</b>: a row's
        /// <see cref="PlayerStatistic.Value"/> is always a genuine recorded value, 0 included. A 0 from
        /// <see cref="GetStatisticValue"/> is therefore ambiguous (an unrecorded stat and a recorded 0 read
        /// alike), so a caller that must tell the two apart — e.g. an "at most" challenge a legitimate 0
        /// should satisfy — branches on this return value, never on the value being 0.
        /// </para>
        /// </summary>
        /// <returns><c>true</c> when a row exists for the (type, entityId) pair; otherwise <c>false</c>.</returns>
        public bool TryGetStatisticValue(EStatisticType type, int? entityId, out decimal value)
        {
            var exists = _statistics.TryGetValue((type, entityId), out var stat);
            value = stat?.Value ?? 0m;
            return exists;
        }

        /// <summary>
        /// Looks up a player's progress in a proficiency, reporting whether a row exists. A missing row is the
        /// "never trained" state (level 0, no XP) — the same row-presence convention statistics use — so the
        /// XP-accrual path treats absence as a fresh start rather than reading a magic value.
        /// </summary>
        /// <returns><c>true</c> when a row exists for the proficiency; otherwise <c>false</c>.</returns>
        public bool TryGetProficiency(int proficiencyId, [MaybeNullWhen(false)] out PlayerProficiency proficiency) =>
            _proficiencies.TryGetValue(proficiencyId, out proficiency);

        /// <summary>
        /// Clears the dirty-row tracking, so a subsequent save of this same aggregate re-enqueues and
        /// re-persists only rows mutated since <em>this</em> point rather than every row dirtied since load.
        /// Called by the repository once a save's envelope has been buffered.
        /// </summary>
        public void AcceptChanges()
        {
            _dirtyStatistics.Clear();
            _dirtyChallenges.Clear();
            _dirtyProficiencies.Clear();
        }

        /// <summary>
        /// Sets a proficiency's absolute level and accumulated XP, creating the row on first training, and
        /// marks it dirty for the write-behind persist. The XP → level computation against the authored curve
        /// belongs to the XP-accrual sub-issue (#1116); this is the persistence seam it writes its result
        /// through (absolute values, so a re-applied write-behind event converges idempotently).
        /// </summary>
        public void SetProficiencyProgress(int proficiencyId, int level, decimal xp)
        {
            if (_proficiencies.TryGetValue(proficiencyId, out var proficiency))
            {
                proficiency.Level = level;
                proficiency.Xp = xp;
            }
            else
            {
                _proficiencies[proficiencyId] = new PlayerProficiency
                {
                    ProficiencyId = proficiencyId,
                    Level = level,
                    Xp = xp,
                };
            }

            _dirtyProficiencies.Add(proficiencyId);
        }

        /// <summary>
        /// Attributes this kill to the damage-type key(s) the battle's <em>majority-dealt</em> leaf type rolls
        /// up to (#1455) — not the literal killing blow's type, which would need new tracking through the
        /// per-portion hit/DoT-tick loop for an ill-defined tie-break on a multi-portion hit. Reusing the
        /// already-accumulated offense book (<see cref="BattleStats.TypedDamageDealt"/>) instead is robust to
        /// last-hit noise and stays a pure post-battle read, matching how this whole statistics pass has no
        /// battle-parity surface. Ties break on the lower <see cref="EDamageType"/> ordinal, for determinism.
        /// Rolled up through <see cref="DamageTypes.Applies"/> like proficiency training, so a Burn-majority
        /// kill books Burn, Fire, Elemental, and Dot alike (backing both "kill with fire magic" and "kill with
        /// a weapon type" challenges — weapon leaves are damage-type leaves).
        /// </summary>
        private void RecordKillByDamageType(BattleStats stats)
        {
            if (stats.TypedDamageDealt.Count == 0)
            {
                return;
            }

            var majorityType = stats.TypedDamageDealt
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => (int)kv.Key)
                .First().Key;

            foreach (var key in DamageTypes.Applies(majorityType))
            {
                Record(EStatisticType.KillsByDamageType, (int)key, 1);
            }
        }

        /// <summary>
        /// Records a reported value for a statistic, aggregating it according to the statistic type's
        /// <see cref="StatisticType.AggregationKind"/>. This single dispatch is the one place a statistic's
        /// aggregation direction is consumed — the same derived fact drives a challenge's goal comparison —
        /// so adding a "lower is better" statistic needs no change here, only its mapping on
        /// <see cref="StatisticType"/>.
        /// </summary>
        private void Record(EStatisticType type, int? entityId, decimal value)
        {
            var aggregationKind = StatisticType.GetAggregationKind(type);
            switch (aggregationKind)
            {
                case EAggregationKind.Sum:
                    Increment(type, entityId, value);
                    break;
                case EAggregationKind.Max:
                    SetMax(type, entityId, value);
                    break;
                case EAggregationKind.Min:
                    SetMin(type, entityId, value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type),
                        $"Statistic {type} has an unhandled aggregation kind {aggregationKind}.");
            }
        }

        private void Increment(EStatisticType type, int? entityId, decimal amount)
        {
            // A zero delta carries no information for a Sum statistic (row absence is the "no data" signal),
            // so skip it outright: no row, no dirty-mark, no touched key re-evaluating the stat's challenges.
            if (amount == 0m)
            {
                return;
            }

            var stat = GetOrCreate(type, entityId, out _);
            stat.Value += amount;
        }

        private void SetMax(EStatisticType type, int? entityId, decimal value)
        {
            var stat = GetOrCreate(type, entityId, out var created);
            // Record on the first write (created) or a strictly greater value, so the first recorded value
            // wins outright instead of racing the 0 a fresh row starts at.
            if (created || value > stat.Value)
            {
                stat.Value = value;
            }
        }

        private void SetMin(EStatisticType type, int? entityId, decimal value)
        {
            var stat = GetOrCreate(type, entityId, out var created);
            // Record on the first write (created) or a strictly lesser value. Keying off the explicit
            // created flag — not "Value == 0" — lets a legitimate 0 minimum lock in rather than being
            // overwritten forever as if it were the empty placeholder.
            if (created || value < stat.Value)
            {
                stat.Value = value;
            }
        }

        private PlayerStatistic GetOrCreate(EStatisticType type, int? entityId, out bool created)
        {
            var key = (type, entityId);
            created = !_statistics.TryGetValue(key, out var stat);
            if (stat is null)
            {
                stat = new PlayerStatistic { Type = type, EntityId = entityId, Value = 0 };
                _statistics[key] = stat;
            }

            // GetOrCreate is reached only from the mutators (Increment/SetMax/SetMin); reads use
            // GetStatisticValue/TryGetStatisticValue, which never land here. So marking dirty here covers
            // exactly the mutated stats. A no-op SetMax/SetMin over-marks at worst, which is harmless — the
            // persist is an idempotent absolute write.
            _dirtyStatistics.Add(key);
            return stat;
        }
    }

    public class CompletedChallenge
    {
        public required int ChallengeId { get; set; }
        public int? RewardItemId { get; set; }
        public int? RewardItemModId { get; set; }
    }
}
