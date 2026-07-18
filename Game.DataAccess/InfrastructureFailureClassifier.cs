using Npgsql;

namespace Game.DataAccess
{
    /// <summary>
    /// Distinguishes a database-<b>infrastructure</b> failure (the connection is down, or a command timed out
    /// reaching it — the whole database is unreachable right now) from a genuine per-write failure (the server
    /// responded with an error specific to this write: a constraint violation, deadlock, serialization failure).
    /// <see cref="DataProviderSynchronizer"/> uses this to decide what an exhausted-retry player-update event
    /// means: a per-write failure is dead-lettered, since only this write is affected, but an infrastructure
    /// failure means every other in-flight write is failing the same way right now, so dead-lettering would
    /// turn a self-resolving outage into a mass manual-replay chore instead.
    /// </summary>
    internal static class InfrastructureFailureClassifier
    {
        public static bool IsInfrastructureFailure(Exception ex) => ex switch
        {
            // A PostgresException is an error response FROM the server, so the connection itself is healthy —
            // the write failed on its own merits (checked before the broader NpgsqlException case below, since
            // PostgresException derives from it).
            PostgresException => false,
            NpgsqlException => true,
            TimeoutException => true,
            _ => ex.InnerException is { } inner && IsInfrastructureFailure(inner),
        };
    }
}
