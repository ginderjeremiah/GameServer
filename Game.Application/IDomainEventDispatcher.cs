using Game.Core.Events;

namespace Game.Application
{
    /// <summary>
    /// Dispatches collected domain events to all registered <see cref="IDomainEventHandler"/>
    /// implementations.
    /// </summary>
    public interface IDomainEventDispatcher
    {
        /// <summary>
        /// Dispatches each event in <paramref name="events"/> to every registered handler.
        /// Handlers silently ignore events whose type they do not handle.
        /// </summary>
        Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default);
    }
}
