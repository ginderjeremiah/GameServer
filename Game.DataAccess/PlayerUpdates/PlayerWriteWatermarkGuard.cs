using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates
{
    /// <summary>
    /// Runs a write-behind handler's apply behind its targets' <see cref="PlayerWriteWatermark"/> rows, so a
    /// write older than what a target already holds is skipped instead of durably regressing it (#2474). This
    /// is what closes the cross-instance half of the stale-absolute-overwrite hazard: an event parked on the
    /// processing list by one instance and reclaimed after another instance already applied the player's next
    /// save would otherwise win simply by being applied last.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The compare <em>is</em> the write.</b> A read-then-compare-then-apply does not survive the race it is
    /// meant to close — two instances both read the same watermark, both pass, and under <c>READ COMMITTED</c>
    /// whichever commits last wins the data row. The conditional upsert below instead takes the watermark row's
    /// lock first and reports which targets it actually advanced, and the data write runs in the same
    /// transaction, so the watermark row is the per-target serialization point.
    /// </para>
    /// <para>
    /// <b>The transaction is load-bearing, not framing.</b> Split into separate commits, a crash between the
    /// watermark advance and the data write would advance the watermark without the data, and the redelivered
    /// event would then be rejected as stale — a silently lost write, strictly worse than the bug being fixed.
    /// </para>
    /// </remarks>
    internal sealed class PlayerWriteWatermarkGuard(GameContext context, PlayerUpdateContext updateContext)
    {
        /// <summary>
        /// The single target key of a genuinely player-scoped stream, where the event's target is the player
        /// row itself and there is nothing finer to key on.
        /// </summary>
        public const string PlayerScopedTarget = "";

        /// <summary>
        /// Advances the watermarks this event is allowed to advance and invokes <paramref name="applyAsync"/>
        /// with exactly those target keys, all inside one transaction. A key whose watermark already holds a
        /// <em>higher</em> sequence is rejected: it is not passed to <paramref name="applyAsync"/>, which must
        /// therefore write only the targets it is handed rather than everything its event carries.
        /// <para>
        /// Rejection is per target, not per event, because a progress event carries only a save's dirty rows —
        /// an all-or-nothing rule would let a newer event covering one statistic discard an older event's
        /// entirely different, still-current statistic. A handler that genuinely needs all-or-nothing (the
        /// equipment stream's item+slot pair) gets it by throwing from <paramref name="applyAsync"/> when the
        /// accepted set is short, which rolls the transaction back with no watermark advanced.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Takes no <c>CancellationToken</c> by design — see <see cref="IPlayerUpdateHandler{TEvent}"/> (#1029).
        /// </remarks>
        public async Task ExecuteGuardedAsync(
            int playerId,
            PlayerWriteStream stream,
            IReadOnlyCollection<string> targetKeys,
            Func<IReadOnlySet<string>, Task> applyAsync)
        {
            try
            {
                await AttemptAsync(playerId, stream, targetKeys, applyAsync);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                // A concurrent apply of the same at-least-once event inserted a row between this attempt's load
                // and its save. The failed save also aborted the surrounding transaction, so the whole attempt
                // restarts — watermark advance included — rather than just the apply re-running inside a
                // transaction that can no longer commit. The now-existing rows load as updates on the second
                // pass, so it carries no conflicting insert; a second failure propagates to the queue's retry
                // policy rather than looping here. Re-reading the watermarks is deliberate: another instance
                // may have advanced them while this attempt was unwinding, and a cached accepted-set would
                // apply against a decision that is no longer true.
                context.ChangeTracker.Clear();
                await AttemptAsync(playerId, stream, targetKeys, applyAsync);
            }
        }

        private async Task AttemptAsync(
            int playerId,
            PlayerWriteStream stream,
            IReadOnlyCollection<string> targetKeys,
            Func<IReadOnlySet<string>, Task> applyAsync)
        {
            // Distinct because ON CONFLICT DO UPDATE cannot affect the same row twice in one statement, and
            // ordered so two events sharing a pair of targets take their row locks in the same order rather
            // than deadlocking against each other.
            var keys = targetKeys.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

            // Sequence 0 is the "unsequenced" sentinel, not the oldest sequence — it is what an envelope
            // enqueued by a pre-upgrade instance mid-rolling-deploy deserializes to. Comparing it as a real
            // sequence would make it lose every comparison, so a player reconnecting onto a pre-upgrade
            // instance after an upgraded one advanced their watermarks would have every guarded write silently
            // discarded for that whole session. It therefore applies unconditionally and leaves the watermarks
            // untouched — exactly the pre-guard behaviour, and the right semantic for an envelope carrying no
            // ordering information. An event with no targets takes the same path: there is nothing to guard.
            if (updateContext.Sequence == DomainEventEnvelope.Unsequenced || keys.Length == 0)
            {
                await applyAsync(keys.ToHashSet(StringComparer.Ordinal));
                return;
            }

            await using var transaction = await context.Database.BeginTransactionAsync();

            var accepted = await AdvanceWatermarksAsync(playerId, stream, keys, updateContext.Sequence);
            if (accepted.Count > 0)
            {
                await applyAsync(accepted);
            }

            await transaction.CommitAsync();

            // Counted only once the transaction has actually committed, so an apply that rolled back doesn't
            // report rejections that were themselves rolled back. The drain surfaces the per-pass total.
            updateContext.RecordRejectedTargets(keys.Length - accepted.Count);
        }

        /// <summary>
        /// Upserts one watermark row per key in <paramref name="keys"/> (already deduplicated and ordered),
        /// advancing only those whose stored sequence is at or below <paramref name="sequence"/>, and returns
        /// the keys it advanced.
        /// </summary>
        private async Task<IReadOnlySet<string>> AdvanceWatermarksAsync(
            int playerId,
            PlayerWriteStream stream,
            string[] keys,
            long sequence)
        {
            // The ORDER BY carries the caller's lock order into the insert itself.
            //
            // The predicate is on the *column* (accept when the stored watermark is at or below this event),
            // which is the same rule as "reject when the event is strictly older" stated from the other side.
            // It must be <=, not <: equal sequences are the same save's sibling writes landing on one target,
            // and a fresh row seeded at this sequence would immediately no-op under <. The cost is that an
            // exact duplicate re-applies rather than being skipped — one redundant no-op write, the correct
            // trade under the queue's at-least-once contract.
            var advanced = await context.Database
                .SqlQueryRaw<string>(
                    """
                    INSERT INTO "PlayerWriteWatermarks" ("PlayerId", "Stream", "TargetKey", "LastAppliedSequence")
                    SELECT {0}, {1}, "Key", {2} FROM unnest({3}) AS t("Key") ORDER BY "Key"
                    ON CONFLICT ("PlayerId", "Stream", "TargetKey") DO UPDATE
                        SET "LastAppliedSequence" = EXCLUDED."LastAppliedSequence"
                        WHERE "PlayerWriteWatermarks"."LastAppliedSequence" <= EXCLUDED."LastAppliedSequence"
                    RETURNING "TargetKey" AS "Value"
                    """,
                    playerId, (int)stream, sequence, keys)
                .ToListAsync();

            return advanced.ToHashSet(StringComparer.Ordinal);
        }
    }
}
