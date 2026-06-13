using Game.Core;

namespace Game.Core.Events
{

    /// <summary>
    /// Dispatches collected domain events to all respective registered handler implementations.
    /// </summary>
    public interface IDomainEventDispatcher
    {
        /// <summary>
        /// Dispatches all events in the <see cref="AggregateRoot.DomainEvents"/> collection of
        /// <paramref name="aggregateRoot"/>, clearing each batch before running its handlers and draining
        /// any events those handlers raise on the aggregate in the same cycle. Each handler invocation is
        /// isolated: one handler throwing does not prevent the remaining handlers or events from being
        /// dispatched. If any handler threw, the collected failures are rethrown as an
        /// <see cref="AggregateException"/> once the aggregate is quiescent.
        /// </summary>
        Task DispatchAsync(AggregateRoot aggregateRoot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dispatches each event in <paramref name="events"/> to every handler registered for that event
        /// type. Each handler invocation is isolated so one handler throwing does not abort the rest of the
        /// batch; if any handler threw, the collected failures are rethrown as an
        /// <see cref="AggregateException"/> after the whole batch has been dispatched.
        /// </summary>
        Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default);
    }
}
