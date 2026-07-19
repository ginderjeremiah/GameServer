using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Abstractions.Infrastructure;
using Game.Core;
using Game.Core.Events;
using Game.Core.Players.Events;
using Game.DataAccess.PlayerUpdates;
using System.Text.Json;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Guarded inspection/replay over the player write-behind dead-letter queue (#794). Inspection is a
    /// non-destructive peek that classifies each entry so an operator can tell poison from replayable.
    /// Replay re-enqueues entries onto the player update queue and wakes the synchronizer; it pushes onto
    /// the destination BEFORE removing from the dead-letter queue, so a crash mid-replay can only duplicate
    /// an entry (absorbed by the idempotent write-behind handlers) and never lose it — the same
    /// at-least-once contract the main queue read has, and why a destructive LPOP-then-push is ruled out.
    /// "Replay all" only re-enqueues entries classified as replayable — a malformed or unknown-type entry
    /// stays queued, mirroring the sibling socket-command DLQ's peek/replay contract — and both push and
    /// removal are single batched round trips regardless of how many entries replay (#2129).
    /// </summary>
    internal sealed class PlayerUpdateDeadLetters(IPubSubService pubsub) : IPlayerUpdateDeadLetters
    {
        // The event-type names the synchronizer knows how to apply: every domain event the persistence
        // publisher enqueues, plus the progress event the progress repo publishes directly. Derived from the
        // publisher's registered handlers rather than hand-listed so it can't drift from what is actually
        // published onto the queue.
        private static readonly IReadOnlySet<string> _knownEventTypes = BuildKnownEventTypes();

        private readonly IPubSubService _pubsub = pubsub;

        public async Task<DeadLetterInspection> InspectAsync(int max)
        {
            var deadLetterQueue = _pubsub.GetQueue(Constants.PUBSUB_PLAYER_DEAD_LETTER_QUEUE);
            var total = await deadLetterQueue.GetLengthAsync();
            var raw = await deadLetterQueue.PeekAsync(max);

            var entries = new List<DeadLetterEntry>(raw.Count);
            for (var index = 0; index < raw.Count; index++)
            {
                entries.Add(Classify(index, raw[index]));
            }

            return new DeadLetterInspection { TotalCount = total, Entries = entries };
        }

        public Task<DeadLetterReplayResult> ReplayAllAsync() => ReplayAsync(requested: null, all: true);

        public Task<DeadLetterReplayResult> ReplaySelectedAsync(IReadOnlyList<string> payloads)
            => ReplayAsync(payloads, all: false);

        private async Task<DeadLetterReplayResult> ReplayAsync(IReadOnlyList<string>? requested, bool all)
        {
            var deadLetterQueue = _pubsub.GetQueue(Constants.PUBSUB_PLAYER_DEAD_LETTER_QUEUE);
            var playerQueue = _pubsub.GetQueue(Constants.PUBSUB_PLAYER_QUEUE);

            // Snapshot the dead-letter queue so a selected replay can only re-enqueue entries that are
            // actually on it (never an arbitrary caller-supplied payload), honouring duplicate multiplicity.
            var snapshot = await deadLetterQueue.PeekAsync(await deadLetterQueue.GetLengthAsync());

            // "Replay all" only targets entries the synchronizer would actually accept — a malformed or
            // unknown-type payload would just bounce straight back here, matching the sibling socket-command
            // DLQ contract that a non-replayable entry stays queued through both peek and replay. A selected
            // replay is a deliberate, classification-visible operator choice (the admin UI warns when the
            // selection includes poison entries), so it is not filtered the same way.
            var targets = all
                ? snapshot.Where(payload => ClassifyPayload(payload).Reason == EDeadLetterReason.Replayable).ToList()
                : FilterToSnapshot(requested ?? [], snapshot);

            var replayed = 0;
            if (targets.Count > 0)
            {
                // Reserve onto the destination (push to the player update queue) BEFORE acknowledging off
                // the source (remove from the dead-letter queue), each in a single batched round trip rather
                // than one push+remove pair per entry. A crash between the two batches never loses an entry
                // — at worst it duplicates one, absorbed by the idempotent handlers (#769) — the same
                // at-least-once contract as the former per-item loop, without its O(N) sequential round trips.
                await playerQueue.AddRangeToQueueAsync(targets);
                replayed = (int)await deadLetterQueue.RemoveRangeAsync(targets);

                // Wake the synchronizer so it drains the re-enqueued items promptly rather than waiting for
                // the next player save. Fire-and-forget: the data is already durably enqueued (#552).
                await _pubsub.Wake(Constants.PUBSUB_PLAYER_CHANNEL);
            }

            var remaining = await deadLetterQueue.GetLengthAsync();
            return new DeadLetterReplayResult { ReplayedCount = replayed, RemainingCount = remaining };
        }

        /// <summary>
        /// Narrows the requested payloads to those present in <paramref name="snapshot"/>, respecting how
        /// many copies of each are actually queued — so a stale or fabricated selection can never push a
        /// message onto the player update queue that was not genuinely dead-lettered.
        /// </summary>
        private static List<string> FilterToSnapshot(IReadOnlyList<string> requested, IReadOnlyList<string> snapshot)
        {
            var available = new Dictionary<string, int>();
            foreach (var entry in snapshot)
            {
                available[entry] = available.GetValueOrDefault(entry) + 1;
            }

            var result = new List<string>(requested.Count);
            foreach (var payload in requested)
            {
                if (available.GetValueOrDefault(payload) > 0)
                {
                    available[payload]--;
                    result.Add(payload);
                }
            }

            return result;
        }

        private static DeadLetterEntry Classify(int index, string raw)
        {
            var (reason, eventType, playerId) = ClassifyPayload(raw);
            return new DeadLetterEntry { Index = index, RawPayload = raw, Reason = reason, EventType = eventType, PlayerId = playerId };
        }

        /// <summary>
        /// The classification core shared by <see cref="Classify"/> (full inspection entries) and the
        /// "replay all" filter, which only needs the <see cref="EDeadLetterReason"/> half.
        /// </summary>
        private static (EDeadLetterReason Reason, string? EventType, int? PlayerId) ClassifyPayload(string raw)
        {
            DomainEventEnvelope? envelope;
            try
            {
                envelope = raw.Deserialize<DomainEventEnvelope>();
            }
            catch (JsonException)
            {
                return (EDeadLetterReason.Malformed, null, null);
            }

            if (envelope is null || string.IsNullOrEmpty(envelope.Type))
            {
                return (EDeadLetterReason.Malformed, null, null);
            }

            var playerId = PlayerUpdateEnvelopeReader.TryReadPlayerIdFromPayload(envelope.Payload);
            var reason = _knownEventTypes.Contains(envelope.Type)
                ? EDeadLetterReason.Replayable
                : EDeadLetterReason.UnknownEventType;
            return (reason, envelope.Type, playerId);
        }

        private static IReadOnlySet<string> BuildKnownEventTypes()
        {
            var known = typeof(PlayerPersistencePublisher).GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
                .Select(i => i.GetGenericArguments()[0].Name)
                .ToHashSet(StringComparer.Ordinal);

            // ProgressUpdatedEvent is a data-tier persistence payload published directly by the progress
            // repo (not an IDomainEvent routed through the publisher), so it is added explicitly.
            known.Add(nameof(ProgressUpdatedEvent));
            return known;
        }
    }
}
