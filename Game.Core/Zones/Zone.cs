namespace Game.Core.Zones
{
    /// <summary>
    /// The gameplay domain model for a zone. It owns the per-zone encounter-level decisions for both
    /// battle-start paths — the random idle spawn (a level rolled within <see cref="LevelMin"/>/
    /// <see cref="LevelMax"/>) and the deterministic dedicated-boss challenge (the fixed
    /// <see cref="BossLevel"/>). Enemy <em>selection</em> (which enemy spawns) stays in the data-access
    /// layer, so this aggregate works with a resolver rather than holding built enemies.
    /// </summary>
    public class Zone
    {
        private readonly int _levelMin;
        private readonly int _levelMax;
        private readonly int _bossLevel;

        public required int Id { get; init; }

        /// <summary>The inclusive lower bound of the random idle encounter-level range. Must be at least 1
        /// and no greater than <see cref="LevelMax"/>.</summary>
        public required int LevelMin
        {
            get => _levelMin;
            init
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(LevelMin), value,
                        $"{nameof(LevelMin)} must be at least 1.");
                }

                _levelMin = value;
                ValidateLevelRange();
            }
        }

        /// <summary>The inclusive upper bound of the random idle encounter-level range. Must be at least 1
        /// and no less than <see cref="LevelMin"/>.</summary>
        public required int LevelMax
        {
            get => _levelMax;
            init
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(LevelMax), value,
                        $"{nameof(LevelMax)} must be at least 1.");
                }

                _levelMax = value;
                ValidateLevelRange();
            }
        }

        /// <summary>The id of this zone's single dedicated boss, fought via the "Challenge Boss" action,
        /// or <c>null</c> when no boss has been authored.</summary>
        public required int? BossEnemyId { get; init; }

        /// <summary>The fixed level the dedicated boss is fought at, independent of the
        /// <see cref="LevelMin"/>/<see cref="LevelMax"/> idle range. Only meaningful when
        /// <see cref="BossEnemyId"/> is set. Must be at least 1.</summary>
        public required int BossLevel
        {
            get => _bossLevel;
            init
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(BossLevel), value,
                        $"{nameof(BossLevel)} must be at least 1.");
                }

                _bossLevel = value;
            }
        }

        /// <summary>The id of the challenge that gates entry to this zone, or <c>null</c> when the zone is
        /// always open (e.g. the starting zone). The zone unlocks once the player completes that challenge
        /// — see <see cref="IsUnlocked"/>. Gating on a challenge (rather than a fixed <c>Order - 1</c>
        /// chain) keeps progression authorable and decoupled from zone ordering.</summary>
        public required int? UnlockChallengeId { get; init; }

        /// <summary>
        /// Whether this zone is unlocked for a player given the set of challenge ids they have completed.
        /// An ungated zone (<see cref="UnlockChallengeId"/> is <c>null</c>) is always unlocked; a gated zone
        /// unlocks once its gating challenge has been completed.
        /// </summary>
        public bool IsUnlocked(IReadOnlySet<int> completedChallengeIds)
        {
            return UnlockChallengeId is not int gateId || completedChallengeIds.Contains(gateId);
        }

        /// <summary>
        /// Rolls a random encounter level within this zone's inclusive
        /// [<see cref="LevelMin"/>, <see cref="LevelMax"/>] range, used for the random idle spawn.
        /// </summary>
        public int RollEncounterLevel()
        {
            return Random.Shared.Next(LevelMin, LevelMax + 1);
        }

        /// <summary>
        /// Enforces the <see cref="LevelMin"/> &lt;= <see cref="LevelMax"/> invariant. Called from both
        /// bounds' <c>init</c> accessors so a mis-authored range is rejected at construction (with the
        /// offending values named) rather than throwing mid-battle in <see cref="RollEncounterLevel"/>.
        /// The check runs only once both bounds are assigned: a not-yet-set bound has its backing field at
        /// the default <c>0</c>, which a valid level (always &gt;= 1) never is, so the first accessor is a
        /// no-op and the second performs the comparison regardless of initializer order.
        /// </summary>
        private void ValidateLevelRange()
        {
            if (_levelMin > 0 && _levelMax > 0 && _levelMin > _levelMax)
            {
                throw new ArgumentException(
                    $"{nameof(LevelMin)} ({_levelMin}) cannot be greater than {nameof(LevelMax)} ({_levelMax}).",
                    nameof(LevelMin));
            }
        }
    }
}
