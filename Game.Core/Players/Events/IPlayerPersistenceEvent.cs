using Game.Core.Events;

namespace Game.Core.Players.Events
{
    /// <summary>
    /// Marker interface for player domain events that must be persisted to the database
    /// via the write-behind cache mechanism.
    /// </summary>
    public interface IPlayerPersistenceEvent : IDomainEvent { }
}
