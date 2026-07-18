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
            // A PostgresException is an error response FROM the server, so the connection itself is usually
            // healthy and the write failed on its own merits — except a handful of SQLSTATEs where the server
            // itself reports it can't currently do work (a graceful failover/restart, or a reconnect storm on
            // recovery), which is exactly the infrastructure-wide, self-resolving condition this classifier
            // exists to catch. Checked before the broader NpgsqlException case below, since PostgresException
            // derives from it.
            PostgresException { SqlState: { } sqlState } when IsInfrastructureSqlState(sqlState) => true,
            PostgresException => false,
            NpgsqlException => true,
            TimeoutException => true,
            _ => ex.InnerException is { } inner && IsInfrastructureFailure(inner),
        };

        /// <summary>
        /// SQLSTATEs where Postgres itself reports it can't currently accept/complete work, rather than
        /// rejecting this specific write. Deliberately narrower than the full connection_exception (08) and
        /// operator_intervention (57) classes: <see cref="PostgresErrorCodes.QueryCanceled"/> (57014) is a
        /// single statement being cancelled (a lock/statement timeout, or a manual cancel) — not evidence the
        /// database is unreachable — and <see cref="PostgresErrorCodes.DatabaseDropped"/> (57P04) isn't
        /// self-resolving, so parking it would retry forever instead of surfacing to an operator.
        /// </summary>
        private static bool IsInfrastructureSqlState(string sqlState) =>
            sqlState.StartsWith("08", StringComparison.Ordinal) // connection_exception class: the connection itself couldn't be established or was lost
            || sqlState is PostgresErrorCodes.AdminShutdown // 57P01: server is shutting down
                or PostgresErrorCodes.CrashShutdown // 57P02: server crashed
                or PostgresErrorCodes.CannotConnectNow // 57P03: server is starting up / in recovery
                or PostgresErrorCodes.TooManyConnections; // 53300: transient capacity, not a per-write conflict
    }
}
