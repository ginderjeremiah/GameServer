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
        public required int Id { get; init; }
        public required int LevelMin { get; init; }
        public required int LevelMax { get; init; }

        /// <summary>The id of this zone's single dedicated boss, fought via the "Challenge Boss" action,
        /// or <c>null</c> when no boss has been authored.</summary>
        public required int? BossEnemyId { get; init; }

        /// <summary>The fixed level the dedicated boss is fought at, independent of the
        /// <see cref="LevelMin"/>/<see cref="LevelMax"/> idle range. Only meaningful when
        /// <see cref="BossEnemyId"/> is set.</summary>
        public required int BossLevel { get; init; }

        /// <summary>The id of the challenge that gates entry to this zone, or <c>null</c> when the zone is
        /// always open (e.g. the starting zone). The zone unlocks once the player completes that challenge
        /// — see <see cref="IsUnlocked"/>. Gating on a challenge (rather than a fixed <c>Order - 1</c>
        /// chain) keeps progression authorable and decoupled from zone ordering.</summary>
        public required int? UnlockChallengeId { get; init; }

        /// <summary>Whether this zone has a dedicated boss that can be challenged.</summary>
        public bool HasBoss => BossEnemyId.HasValue;

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
    }
}
