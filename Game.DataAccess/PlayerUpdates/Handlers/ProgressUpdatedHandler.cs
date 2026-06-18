using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ProgressUpdatedHandler(GameContext context) : IPlayerUpdateHandler<ProgressUpdatedEvent>
    {
        public async Task HandleAsync(ProgressUpdatedEvent evt)
        {
            // The load-then-upsert isn't atomic, so a concurrent apply of the same at-least-once event can
            // insert a row between our load and save. On the resulting unique violation, clear and re-run
            // once: the now-existing rows load as updates, so the second pass carries no conflicting insert.
            // A second failure propagates to the queue's retry policy rather than looping here.
            try
            {
                await ApplyAsync(evt);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                context.ChangeTracker.Clear();
                await ApplyAsync(evt);
            }
        }

        private async Task ApplyAsync(ProgressUpdatedEvent evt)
        {
            // Absolute upserts so re-applying the event under the retry policy converges to the same state.
            // Batched like the attribute-allocations handler: load the touched rows, set/insert, save once.
            if (evt.Statistics.Count > 0)
            {
                // Bound the load by the touched type id set AND the touched entity id set — their
                // cross-product, which is a superset of the exact (type, entity) pairs changed. Filtering on
                // typeIds alone would, for a long-lived account with one row per enemy/skill, load hundreds to
                // upsert the ~10-20 this battle changed (aggregate-DB-load concern, #548). A value-tuple IN
                // over the exact pairs isn't cleanly EF-translatable, so this cross-product bound is the
                // pragmatic narrowing; the exact-key match still happens in memory below. entityIds includes
                // null for the global rows — EF turns Contains over a List<int?> into
                // "EntityId IN (...) OR EntityId IS NULL".
                var typeIds = evt.Statistics.Select(s => s.StatisticTypeId).Distinct().ToList();
                var entityIds = evt.Statistics.Select(s => s.EntityId).Distinct().ToList();
                var existing = await context.PlayerStatistics
                    .Where(ps => ps.PlayerId == evt.PlayerId
                        && typeIds.Contains(ps.StatisticTypeId)
                        && entityIds.Contains(ps.EntityId))
                    .ToListAsync();
                // Group-by-first rather than ToDictionary: the unique index makes a duplicate (type, entity)
                // impossible, but taking the first per key keeps a stray duplicate row from throwing here and
                // permanently poisoning this player's progress stream.
                var byKey = existing
                    .GroupBy(ps => (ps.StatisticTypeId, ps.EntityId))
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var stat in evt.Statistics)
                {
                    if (byKey.TryGetValue((stat.StatisticTypeId, stat.EntityId), out var row))
                    {
                        row.Value = stat.Value;
                    }
                    else
                    {
                        context.PlayerStatistics.Add(new PlayerStatistic
                        {
                            PlayerId = evt.PlayerId,
                            StatisticTypeId = stat.StatisticTypeId,
                            EntityId = stat.EntityId,
                            Value = stat.Value,
                        });
                    }
                }
            }

            if (evt.Challenges.Count > 0)
            {
                var challengeIds = evt.Challenges.Select(c => c.ChallengeId).ToList();
                var existing = await context.PlayerChallenges
                    .Where(pc => pc.PlayerId == evt.PlayerId && challengeIds.Contains(pc.ChallengeId))
                    .ToListAsync();
                // Group-by-first for the same reason as the statistics lookup above: the (player, challenge)
                // primary key makes a duplicate impossible, but defending the grouping keeps a stray duplicate
                // from throwing and poisoning the stream.
                var byId = existing
                    .GroupBy(pc => pc.ChallengeId)
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var challenge in evt.Challenges)
                {
                    if (byId.TryGetValue(challenge.ChallengeId, out var row))
                    {
                        row.Progress = challenge.Progress;
                        row.Completed = challenge.Completed;
                        row.CompletedAt = challenge.CompletedAt;
                    }
                    else
                    {
                        context.PlayerChallenges.Add(new PlayerChallenge
                        {
                            PlayerId = evt.PlayerId,
                            ChallengeId = challenge.ChallengeId,
                            Progress = challenge.Progress,
                            Completed = challenge.Completed,
                            CompletedAt = challenge.CompletedAt,
                        });
                    }
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
