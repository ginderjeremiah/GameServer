using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.Infrastructure;
using Game.Api.Sockets.Commands;
using Game.Core;
using System.Text.Json;

namespace Game.Api.Services.Admin
{
    /// <summary>
    /// Guarded inspection/replay over the socket command dead-letter queue (#1542), mirroring
    /// <see cref="Game.Abstractions.DataAccess.Admin.IPlayerUpdateDeadLetters"/> for the player write-behind
    /// queue and reusing its contracts (<see cref="DeadLetterEntry"/> etc.) — an entry's <c>EventType</c>
    /// holds the command's type name here. This lives in <c>Game.Api</c> rather than the data tier: replay
    /// dispatches through <see cref="SocketManagerService.EmitSocketCommand(SocketCommandInfo, int)"/> (a
    /// live-socket concern, not a persistence one) and classification reads <see cref="SocketCommandFactory"/>
    /// — both Api-tier types.
    /// </summary>
    public class SocketCommandDeadLetters(IPubSubService pubsub, SocketManagerService socketManager, SocketCommandFactory commandFactory)
    {
        private readonly IPubSubService _pubsub = pubsub;
        private readonly SocketManagerService _socketManager = socketManager;
        private readonly SocketCommandFactory _commandFactory = commandFactory;

        public async Task<DeadLetterInspection> InspectAsync(int max)
        {
            var deadLetterQueue = _pubsub.GetQueue(Constants.PUBSUB_SOCKET_DEAD_LETTER_QUEUE);
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

        /// <summary>
        /// Redelivers each targeted entry via <see cref="SocketManagerService.EmitSocketCommand(SocketCommandInfo, int)"/>
        /// — which resolves whatever socket is currently live for the envelope's player, since the original
        /// socket id is never preserved and may no longer exist — then removes it from the dead-letter queue.
        /// An entry that is not genuinely replayable (malformed, an unrecognized command name, or the player
        /// has no live socket right now) is left queued rather than silently discarded: there is no
        /// destination queue to "reserve onto" first the way the player write-behind replay does, so a
        /// redelivery only counts once it has actually happened.
        /// </summary>
        private async Task<DeadLetterReplayResult> ReplayAsync(IReadOnlyList<string>? requested, bool all)
        {
            var deadLetterQueue = _pubsub.GetQueue(Constants.PUBSUB_SOCKET_DEAD_LETTER_QUEUE);

            // Snapshot the dead-letter queue so a selected replay can only re-enqueue entries that are
            // actually on it (never an arbitrary caller-supplied payload), honouring duplicate multiplicity.
            var snapshot = await deadLetterQueue.PeekAsync(await deadLetterQueue.GetLengthAsync());
            var targets = all ? snapshot : FilterToSnapshot(requested ?? [], snapshot);

            var replayed = 0;
            foreach (var payload in targets)
            {
                var envelope = TryDeserialize(payload);
                // Malformed, UnknownEventType, and NotReplayable entries are poison — the same classification
                // Classify computes — so they stay queued rather than being delivered and dropped; only
                // Replayable (a command that has opted into IReplayableServerCommand) reaches
                // EmitSocketCommand, whose bool result gates the removal on the same live-socket lookup rather
                // than a separate check-then-act pair.
                if (envelope is null || !_commandFactory.IsReplayable(envelope.Command.Name))
                {
                    continue;
                }

                if (!await _socketManager.EmitSocketCommand(envelope.Command, envelope.PlayerId))
                {
                    continue;
                }

                if (await deadLetterQueue.RemoveAsync(payload))
                {
                    replayed++;
                }
            }

            var remaining = await deadLetterQueue.GetLengthAsync();
            return new DeadLetterReplayResult { ReplayedCount = replayed, RemainingCount = remaining };
        }

        /// <summary>
        /// Narrows the requested payloads to those present in <paramref name="snapshot"/>, respecting how
        /// many copies of each are actually queued — so a stale or fabricated selection can never trigger a
        /// redelivery that was not genuinely dead-lettered.
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

        private DeadLetterEntry Classify(int index, string raw)
        {
            var entry = new DeadLetterEntry { Index = index, RawPayload = raw };

            var envelope = TryDeserialize(raw);
            if (envelope is null)
            {
                entry.Reason = EDeadLetterReason.Malformed;
                return entry;
            }

            entry.EventType = envelope.Command.Name;
            entry.PlayerId = envelope.PlayerId;
            entry.Reason = !_commandFactory.IsServerInitiatedOnly(envelope.Command.Name)
                ? EDeadLetterReason.UnknownEventType
                : _commandFactory.IsReplayable(envelope.Command.Name)
                    ? EDeadLetterReason.Replayable
                    : EDeadLetterReason.NotReplayable;
            return entry;
        }

        private static SocketCommandDeadLetterEnvelope? TryDeserialize(string raw)
        {
            try
            {
                var envelope = raw.Deserialize<SocketCommandDeadLetterEnvelope>();
                // `required` only guarantees the "command" key is present, not that its value is non-null
                // (a {"command":null} payload deserializes fine) — guard it explicitly rather than letting an
                // NRE on envelope.Command.Name escape this method uncaught.
                return envelope?.Command is null || string.IsNullOrEmpty(envelope.Command.Name) ? null : envelope;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
