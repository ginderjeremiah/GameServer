using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Game.DataAccess
{
    internal static class DbUpdateExceptionExtensions
    {
        /// <summary>
        /// True when the failed save was rejected by a unique constraint — the signal that a concurrent
        /// request inserted a row with the same natural key between this request's read and its save. Both
        /// the get-or-create login paths and the at-least-once write-behind handlers rest on a non-atomic
        /// existence-check-then-insert, so they key their conflict handling off this (reload-and-retry, or
        /// treat the duplicate as a benign no-op against the table's existing unique index).
        /// </summary>
        public static bool IsUniqueViolation(this DbUpdateException ex)
        {
            return ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
        }
    }
}
