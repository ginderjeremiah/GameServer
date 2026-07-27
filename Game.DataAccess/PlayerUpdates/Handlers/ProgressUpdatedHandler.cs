using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class ProgressUpdatedHandler(PlayerWriteWatermarkGuard guard) : IPlayerUpdateHandler<ProgressUpdatedEvent>
    {
        // Keyed per row rather than per player: a progress event carries only a save's dirty rows, so a
        // per-player watermark would let a newer event covering statistic Y reject an older event carrying an
        // entirely different, still-current statistic X — silently losing writes on the game's highest-volume
        // persistence path. The kind prefix keeps the three row families from colliding in one key space, and
        // the ids are formatted invariantly because the key is a persisted comparison key — a culture that
        // renders digits differently would write a second watermark row and the guard would silently stop
        // seeing the first.
        private static string StatisticTarget(CachedPlayerStatistic stat)
            => FormattableString.Invariant($"stat:{stat.StatisticTypeId}:{stat.EntityId}");
        private static string ChallengeTarget(CachedPlayerChallenge challenge)
            => FormattableString.Invariant($"challenge:{challenge.ChallengeId}");
        private static string ProficiencyTarget(CachedPlayerProficiency proficiency)
            => FormattableString.Invariant($"prof:{proficiency.ProficiencyId}");

        public Task HandleAsync(ProgressUpdatedEvent evt)
        {
            var targets = evt.Statistics.Select(StatisticTarget)
                .Concat(evt.Challenges.Select(ChallengeTarget))
                .Concat(evt.Proficiencies.Select(ProficiencyTarget))
                .ToList();

            // The guard owns the transaction and the unique-violation restart the load-then-upsert below needs
            // (a concurrent apply of the same at-least-once event can insert a row between the load and the
            // save), so this handler only has to write the rows it was handed — through the context the guard
            // passes it, which is the one its transaction covers.
            return guard.ExecuteGuardedAsync(evt.PlayerId, PlayerWriteStream.Progress, targets, (context, accepted) => ApplyAsync(context, evt, accepted));
        }

        private static async Task ApplyAsync(GameContext context, ProgressUpdatedEvent evt, IReadOnlySet<string> accepted)
        {
            // Absolute upserts so re-applying the event under the retry policy converges to the same state.
            // Batched like the attribute-allocations handler: load the touched rows, set/insert, save once.
            // Only the accepted targets are written — the rest are already superseded by a newer event.
            var statistics = evt.Statistics.Where(s => accepted.Contains(StatisticTarget(s))).ToList();
            if (statistics.Count > 0)
            {
                // Bound the load by the touched type id set AND the touched entity id set — their
                // cross-product, which is a superset of the exact (type, entity) pairs changed. Filtering on
                // typeIds alone would, for a long-lived account with one row per enemy/skill, load hundreds to
                // upsert the ~10-20 this battle changed (aggregate-DB-load concern, #548). A value-tuple IN
                // over the exact pairs isn't cleanly EF-translatable, so this cross-product bound is the
                // pragmatic narrowing; the exact-key match still happens in memory below. entityIds includes
                // null for the global rows — EF turns Contains over a List<int?> into
                // "EntityId IN (...) OR EntityId IS NULL".
                var typeIds = statistics.Select(s => s.StatisticTypeId).Distinct().ToList();
                var entityIds = statistics.Select(s => s.EntityId).Distinct().ToList();
                var existing = await context.PlayerStatistics
                    .Where(ps => ps.PlayerId == evt.PlayerId
                        && typeIds.Contains(ps.StatisticTypeId)
                        && entityIds.Contains(ps.EntityId))
                    .ToListAsync();
                // The unique index makes a duplicate (type, entity) impossible; ToFirstByKey still defends
                // against a stray duplicate row throwing here and poisoning this player's progress stream.
                var byKey = existing.ToFirstByKey(ps => (ps.StatisticTypeId, ps.EntityId));

                foreach (var stat in statistics)
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

            var challenges = evt.Challenges.Where(c => accepted.Contains(ChallengeTarget(c))).ToList();
            if (challenges.Count > 0)
            {
                var challengeIds = challenges.Select(c => c.ChallengeId).ToList();
                var existing = await context.PlayerChallenges
                    .Where(pc => pc.PlayerId == evt.PlayerId && challengeIds.Contains(pc.ChallengeId))
                    .ToListAsync();
                // Same defensive grouping as the statistics lookup above: the (player, challenge) primary key
                // makes a duplicate impossible, but ToFirstByKey keeps a stray duplicate from poisoning it.
                var byId = existing.ToFirstByKey(pc => pc.ChallengeId);

                foreach (var challenge in challenges)
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

            var proficiencies = evt.Proficiencies.Where(p => accepted.Contains(ProficiencyTarget(p))).ToList();
            if (proficiencies.Count > 0)
            {
                var proficiencyIds = proficiencies.Select(p => p.ProficiencyId).ToList();
                var existing = await context.PlayerProficiencies
                    .Where(pp => pp.PlayerId == evt.PlayerId && proficiencyIds.Contains(pp.ProficiencyId))
                    .ToListAsync();
                // Same defensive grouping as the challenge lookup above: the (player, proficiency) primary key
                // makes a duplicate impossible, but ToFirstByKey keeps a stray duplicate from poisoning it.
                var byId = existing.ToFirstByKey(pp => pp.ProficiencyId);

                foreach (var proficiency in proficiencies)
                {
                    if (byId.TryGetValue(proficiency.ProficiencyId, out var row))
                    {
                        row.Level = proficiency.Level;
                        row.Xp = proficiency.Xp;
                    }
                    else
                    {
                        context.PlayerProficiencies.Add(new PlayerProficiency
                        {
                            PlayerId = evt.PlayerId,
                            ProficiencyId = proficiency.ProficiencyId,
                            Level = proficiency.Level,
                            Xp = proficiency.Xp,
                        });
                    }
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
