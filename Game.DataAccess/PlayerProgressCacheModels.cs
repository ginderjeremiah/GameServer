namespace Game.DataAccess
{
    /// <summary>
    /// Serializable snapshot of a player's progress as cached under the <c>Progress_{id}</c> key — the
    /// source of truth for statistics and challenge progress. The owning player is cached separately, so it
    /// is intentionally not carried here, and challenges are stored as their progress rows (the authored
    /// <c>Challenge</c> is resolved from the in-memory reference cache on load).
    /// </summary>
    internal sealed class CachedPlayerProgress
    {
        public List<CachedPlayerStatistic> Statistics { get; set; } = [];
        public List<CachedPlayerChallenge> Challenges { get; set; } = [];
    }

    internal sealed class CachedPlayerStatistic
    {
        public int StatisticTypeId { get; set; }
        public int? EntityId { get; set; }
        public decimal Value { get; set; }
    }

    internal sealed class CachedPlayerChallenge
    {
        public int ChallengeId { get; set; }
        public decimal Progress { get; set; }
        public bool Completed { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// Write-behind persistence event for player progress: the stat and challenge rows that changed in one
    /// save, carried as <b>absolute</b> values so the consumer (<see cref="DataProviderSynchronizer"/>) can
    /// upsert them idempotently under the queue's retry policy. Published directly by
    /// <see cref="Repositories.PlayerProgressRepository"/> onto the player update queue — the progress
    /// aggregate is not routed through the domain-event dispatcher.
    /// </summary>
    internal sealed class ProgressUpdatedEvent
    {
        public int PlayerId { get; set; }
        public List<CachedPlayerStatistic> Statistics { get; set; } = [];
        public List<CachedPlayerChallenge> Challenges { get; set; } = [];
    }
}
