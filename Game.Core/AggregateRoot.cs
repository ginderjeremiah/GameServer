using Game.Core.Events;
using System.Collections.ObjectModel;

namespace Game.Core
{
    /// <summary>
    /// Base class for domain aggregates that can raise domain events.
    /// Domain events are collected during an operation and dispatched by the application
    /// layer after all domain changes are complete.
    /// </summary>
    public abstract class AggregateRoot
    {
        private readonly List<IDomainEvent> _domainEvents = [];

        // A ReadOnlyCollection is a live view over the backing list, so the wrapper is built once and reused
        // rather than re-allocated on every get — the dispatcher reads Count then ToArray per drain iteration.
        private readonly ReadOnlyCollection<IDomainEvent> _domainEventsView;

        protected AggregateRoot()
        {
            _domainEventsView = _domainEvents.AsReadOnly();
        }

        /// <summary>
        /// Domain events raised during the current operation.
        /// </summary>
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEventsView;

        /// <summary>
        /// Records a domain event to be dispatched by the application layer.
        /// </summary>
        protected void RaiseEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

        /// <summary>
        /// Clears all collected domain events. Called by the application layer after dispatch.
        /// </summary>
        public void ClearEvents() => _domainEvents.Clear();
    }
}
