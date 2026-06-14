using Game.Core;
using Game.Core.Events;
using Game.Core.Players.Events;

namespace Game.DataAccess
{
    /// <summary>
    /// Bridges player domain events into the Redis write-behind queue consumed by
    /// <see cref="DataProviderSynchronizer"/>. Registered (in <c>AddDataAccess</c>) against each
    /// player event whose change must be persisted to the database; events that are only relevant
    /// in-process (e.g. <c>PlayerLeveledUpEvent</c>) are deliberately not registered here.
    /// <para>
    /// Rather than publishing per event, it buffers each event's envelope into the scoped
    /// <see cref="PlayerUpdateBatch"/>; <c>PlayerRepository.SavePlayer</c> flushes the whole batch as one
    /// multi-value LPUSH after the dispatch settles, so a save raising several player events costs a single
    /// queue round-trip instead of one per event (#559).
    /// </para>
    /// </summary>
    internal class PlayerPersistencePublisher(PlayerUpdateBatch batch) :
        IDomainEventHandler<PlayerCoreUpdatedEvent>,
        IDomainEventHandler<AttributeAllocationsChangedEvent>,
        IDomainEventHandler<ItemUnlockedEvent>,
        IDomainEventHandler<ItemEquippedEvent>,
        IDomainEventHandler<ItemUnequippedEvent>,
        IDomainEventHandler<ModUnlockedEvent>,
        IDomainEventHandler<ModAppliedEvent>,
        IDomainEventHandler<ModRemovedEvent>,
        IDomainEventHandler<SkillUnlockedEvent>,
        IDomainEventHandler<SelectedSkillsChangedEvent>,
        IDomainEventHandler<ItemFavoriteChangedEvent>,
        IDomainEventHandler<LogPreferenceChangedEvent>
    {
        private readonly PlayerUpdateBatch _batch = batch;

        public Task HandleAsync(PlayerCoreUpdatedEvent domainEvent, CancellationToken cancellationToken = default) => Buffer(domainEvent);
        public Task HandleAsync(AttributeAllocationsChangedEvent domainEvent, CancellationToken cancellationToken = default) => Buffer(domainEvent);
        public Task HandleAsync(ItemUnlockedEvent domainEvent, CancellationToken cancellationToken = default) => Buffer(domainEvent);
        public Task HandleAsync(ItemEquippedEvent domainEvent, CancellationToken cancellationToken = default) => Buffer(domainEvent);
        public Task HandleAsync(ItemUnequippedEvent domainEvent, CancellationToken cancellationToken = default) => Buffer(domainEvent);
        public Task HandleAsync(ModUnlockedEvent domainEvent, CancellationToken cancellationToken = default) => Buffer(domainEvent);
        public Task HandleAsync(ModAppliedEvent domainEvent, CancellationToken cancellationToken = default) => Buffer(domainEvent);
        public Task HandleAsync(ModRemovedEvent domainEvent, CancellationToken cancellationToken = default) => Buffer(domainEvent);
        public Task HandleAsync(SkillUnlockedEvent domainEvent, CancellationToken cancellationToken = default) => Buffer(domainEvent);
        public Task HandleAsync(SelectedSkillsChangedEvent domainEvent, CancellationToken cancellationToken = default) => Buffer(domainEvent);
        public Task HandleAsync(ItemFavoriteChangedEvent domainEvent, CancellationToken cancellationToken = default) => Buffer(domainEvent);
        public Task HandleAsync(LogPreferenceChangedEvent domainEvent, CancellationToken cancellationToken = default) => Buffer(domainEvent);

        private Task Buffer<T>(T domainEvent) where T : IDomainEvent
        {
            _batch.Add(new DomainEventEnvelope
            {
                Type = domainEvent.GetType().Name,
                Payload = domainEvent.Serialize(),
            });

            return Task.CompletedTask;
        }
    }
}
