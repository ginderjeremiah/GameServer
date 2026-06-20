using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Abstractions.Infrastructure;
using Game.Core;
using Game.Core.Events;
using Game.Core.Players.Events;
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
            var targets = all ? snapshot : FilterToSnapshot(requested ?? [], snapshot);

            var replayed = 0;
            foreach (var payload in targets)
            {
                // Reserve onto the destination (push to the player update queue) BEFORE acknowledging off
                // the source (remove from the dead-letter queue). A crash between the two re-queues, never
                // loses, the entry; the idempotent handlers absorb the rare duplicate (#769).
                await playerQueue.AddToQueueAsync(payload);
                if (await deadLetterQueue.RemoveAsync(payload))
                {
                    replayed++;
                }
            }

            if (replayed > 0)
            {
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
            var entry = new DeadLetterEntry { Index = index, RawPayload = raw };

            DomainEventEnvelope? envelope;
            try
            {
                envelope = raw.Deserialize<DomainEventEnvelope>();
            }
            catch (JsonException)
            {
                entry.Reason = EDeadLetterReason.Malformed;
                return entry;
            }

            if (envelope is null || string.IsNullOrEmpty(envelope.Type))
            {
                entry.Reason = EDeadLetterReason.Malformed;
                return entry;
            }

            entry.EventType = envelope.Type;
            entry.PlayerId = TryReadPlayerId(envelope.Payload);
            entry.Reason = _knownEventTypes.Contains(envelope.Type)
                ? EDeadLetterReason.Replayable
                : EDeadLetterReason.UnknownEventType;
            return entry;
        }

        /// <summary>
        /// Pulls the owning player id out of an event payload generically (every persisted player event
        /// carries a <c>playerId</c>), without coupling to each concrete event type. Returns null when the
        /// inner payload is malformed or has no numeric player id.
        /// </summary>
        private static int? TryReadPlayerId(string? payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("playerId", out var property)
                    && property.ValueKind == JsonValueKind.Number
                    && property.TryGetInt32(out var playerId))
                {
                    return playerId;
                }
            }
            catch (JsonException)
            {
                // A malformed inner payload simply has no derivable player id.
            }

            return null;
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
