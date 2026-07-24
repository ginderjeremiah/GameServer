using Game.Core;
using System.Text.Json;

namespace Game.DataAccess.PlayerUpdates
{
    /// <summary>
    /// Parses a raw queued envelope and reads the owning player id out of it generically (every persisted
    /// player-update event's payload carries a <c>playerId</c>), without coupling to each concrete event type.
    /// Shared by the dead-letter inspector (<see cref="Repositories.Admin.PlayerUpdateDeadLetters"/>, classifying
    /// entries for display) and <see cref="DataProviderSynchronizer"/> (routing reserved items to a per-player
    /// ordering lane for bounded cross-player concurrency, #1701, and threading the one parsed envelope through
    /// to <c>ProcessMessage</c> rather than re-deserializing it). The player-id read is a best-effort peek, not
    /// an authoritative parse: a malformed payload simply yields no player id here, and is left to whichever
    /// caller owns the authoritative parse (the dead-letter classifier, or the event dispatcher's own inner
    /// deserialize) to treat it as a poison message.
    /// </summary>
    internal static class PlayerUpdateEnvelopeReader
    {
        public static (DomainEventEnvelope? Envelope, JsonException? ParseError) TryParseEnvelope(string rawMessage)
        {
            try
            {
                return (rawMessage.Deserialize<DomainEventEnvelope>(), null);
            }
            catch (JsonException ex)
            {
                return (null, ex);
            }
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
