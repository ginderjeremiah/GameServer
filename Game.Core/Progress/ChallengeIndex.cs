namespace Game.Core.Progress
{
    /// <summary>
    /// A precomputed reverse index from the (statistic type, target entity) a challenge tracks to the
    /// challenges tracking it. Battle completion can then evaluate only the challenges whose statistic
    /// actually moved this battle, rather than re-scanning the whole authored catalog every battle — work
    /// that would otherwise grow unbounded with authored content on the per-player hot command path.
    /// <para>
    /// Built once per reference-data snapshot and shared immutably (alongside the cached challenge list),
    /// so the per-battle cost is the lookup, not the indexing.
    /// </para>
    /// </summary>
    public sealed class ChallengeIndex
    {
        private readonly IReadOnlyDictionary<(EStatisticType Type, int? EntityId), IReadOnlyList<Challenge>> _byStatistic;

        // Challenges not keyed to a statistic (today only LevelReached, which reads the player's level
        // directly rather than a recorded statistic). A victory grants experience and can level the player
        // up, so these are relevant to every completed battle regardless of which statistics moved; the set
        // is small and bounded by the number of authored statistic-independent challenges.
        private readonly IReadOnlyList<Challenge> _statisticIndependent;

        public ChallengeIndex(IEnumerable<Challenge> challenges)
        {
            var byStatistic = new Dictionary<(EStatisticType, int?), List<Challenge>>();
            var statisticIndependent = new List<Challenge>();

            foreach (var challenge in challenges)
            {
                if (challenge.Type.StatisticType is { } statisticType)
                {
                    // Index by the exact (statistic, entity) pair the challenge reads (see
                    // Challenge.UpdateChallengeProgress), so a per-entity challenge is evaluated only when
                    // that entity's statistic moved, not whenever any battle touches the statistic type.
                    var key = (statisticType.Id, challenge.TargetEntityId);
                    if (!byStatistic.TryGetValue(key, out var bucket))
                    {
                        bucket = [];
                        byStatistic[key] = bucket;
                    }

                    bucket.Add(challenge);
                }
                else
                {
                    statisticIndependent.Add(challenge);
                }
            }

            _byStatistic = byStatistic.ToDictionary(
                entry => entry.Key, entry => (IReadOnlyList<Challenge>)entry.Value);
            _statisticIndependent = statisticIndependent;
        }

        /// <summary>
        /// The challenges to evaluate after a battle that changed the given statistics: every challenge
        /// keyed to one of the touched (statistic, entity) pairs, plus the statistic-independent challenges.
        /// </summary>
        public IEnumerable<Challenge> RelevantTo(IReadOnlyCollection<(EStatisticType Type, int? EntityId)> touchedStatistics)
        {
            foreach (var challenge in _statisticIndependent)
            {
                yield return challenge;
            }

            // The touched keys are distinct (a set of mutated statistic rows) and each challenge lives in a
            // single bucket, so no challenge is yielded twice.
            foreach (var key in touchedStatistics)
            {
                if (_byStatistic.TryGetValue(key, out var bucket))
                {
                    foreach (var challenge in bucket)
                    {
                        yield return challenge;
                    }
                }
            }
        }
    }
}
