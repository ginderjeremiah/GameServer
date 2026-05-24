using Game.Core.Events;

namespace Game.Application
{
    /// <summary>
    /// Dispatches domain events to all registered <see cref="IDomainEventHandler"/> instances.
    /// Handlers are resolved from DI as <c>IEnumerable&lt;IDomainEventHandler&gt;</c>, so every
    /// handler registered against that interface receives every event. Typed handlers
    /// (<see cref="IDomainEventHandler{TEvent}"/>) use their default interface implementation to
    /// silently skip events that don't match their type.
    /// </summary>
    public class DomainEventDispatcher(IEnumerable<IDomainEventHandler> handlers) : IDomainEventDispatcher
    {
        private readonly IReadOnlyList<IDomainEventHandler> _handlers = handlers.ToList();

        public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        {
            foreach (var domainEvent in events)
                foreach (var handler in _handlers)
                    await handler.HandleAsync(domainEvent, cancellationToken);
        }
    }
}
