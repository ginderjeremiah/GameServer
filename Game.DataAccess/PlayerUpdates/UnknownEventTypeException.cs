namespace Game.DataAccess.PlayerUpdates
{
    /// <summary>
    /// Raised when a dequeued <see cref="DomainEventEnvelope"/> carries a type name that no
    /// <see cref="IPlayerUpdateHandler{TEvent}"/> is registered for. It is a poison message — no retry can
    /// fix it — so <see cref="DataProviderSynchronizer"/> dead-letters it for inspection.
    /// </summary>
    internal sealed class UnknownEventTypeException(string eventType)
        : Exception($"Unrecognized player event type '{eventType}'.");
}
