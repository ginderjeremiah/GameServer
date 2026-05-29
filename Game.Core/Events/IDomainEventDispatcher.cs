using Game.Core;

namespace Game.Core.Events
{

    /// <summary>
    /// Dispatches collected domain events to all respective registered handler implementations.
    /// </summary>
    public interface IDomainEventDispatcher
    {
        /// <summary>
        /// Dispatches all events in the <see cref="AggregateRoot.DomainEvents"/> collection of <paramref name="aggregateRoot"/> and then clears the collection.
        /// </summary>
        /// <param name="aggregateRoot"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task DispatchAsync(AggregateRoot aggregateRoot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dispatches each event in <paramref name="events"/> to every handler registed for that event type.
        /// </summary>
        Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default);
    }
}
