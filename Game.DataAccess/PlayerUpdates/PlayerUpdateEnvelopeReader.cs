using Game.Core;
using System.Text.Json;

namespace Game.DataAccess.PlayerUpdates
{
    /// <summary>
    /// Reads the owning player id out of a raw queued envelope generically (every persisted player-update
    /// event's payload carries a <c>playerId</c>), without coupling to each concrete event type. Shared by the
    /// dead-letter inspector (<see cref="Repositories.Admin.PlayerUpdateDeadLetters"/>, classifying entries for
    /// display) and <see cref="DataProviderSynchronizer"/> (routing reserved items to a per-player ordering lane
    /// for bounded cross-player concurrency, #1701). This is a best-effort peek, not an authoritative parse: a
    /// malformed envelope or payload simply yields no player id here, and is left to whichever caller owns the
    /// authoritative parse (the dead-letter classifier, or <c>ProcessMessage</c>'s own deserialize) to treat it
    /// as a poison message.
    /// </summary>
    internal static class PlayerUpdateEnvelopeReader
    {
        public static int? TryReadPlayerId(string rawMessage)
        {
            DomainEventEnvelope? envelope;
            try
            {
                envelope = rawMessage.Deserialize<DomainEventEnvelope>();
            }
            catch (JsonException)
            {
                return null;
            }

            return envelope is null ? null : TryReadPlayerIdFromPayload(envelope.Payload);
        }

        public static int? TryReadPlayerIdFromPayload(string? payload)
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
    }
}
