using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    /// <summary>
    /// Helpers for the write-behind handlers whose idempotency rests on an existence-check-then-insert (or
    /// load-then-insert) that is not atomic. The queue read is at-least-once, so two applies of the same event
    /// can race — both pass the check and both insert — surfacing a Postgres unique-constraint violation.
    /// Treating that violation as a benign no-op keeps the re-apply idempotent: the row the loser would have
    /// written already exists (with identical content for the constant-row inserts), backed by the table's
    /// existing unique key/index.
    /// </summary>
    internal static class UpsertConcurrency
    {
        public static bool IsUniqueViolation(this DbUpdateException ex)
            => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }
}
