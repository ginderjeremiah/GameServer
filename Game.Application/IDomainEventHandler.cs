using Game.Core.Events;

namespace Game.Application
{
    /// <summary>
    /// Non-generic base for all domain event handlers. Handlers are registered against this
    /// interface in the DI container so the dispatcher can resolve them all at once.
    /// </summary>
    public interface IDomainEventHandler
    {
        Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Typed domain event handler. Provides a default implementation of
    /// <see cref="IDomainEventHandler.HandleAsync"/> that filters by event type so that a
    /// handler only reacts to events it knows about.
    /// </summary>
    public interface IDomainEventHandler<TEvent> : IDomainEventHandler where TEvent : IDomainEvent
    {
        /// <summary>Handles the strongly-typed <typeparamref name="TEvent"/>.</summary>
        Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Default implementation — delegates to the typed overload when the event matches,
        /// otherwise does nothing.
        /// </summary>
        Task IDomainEventHandler.HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
            => domainEvent is TEvent typed ? HandleAsync(typed, cancellationToken) : Task.CompletedTask;
    }
}
