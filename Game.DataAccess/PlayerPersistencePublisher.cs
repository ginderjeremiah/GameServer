using Game.Abstractions.Infrastructure;
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
    /// </summary>
    internal class PlayerPersistencePublisher(IPubSubService pubsub) :
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
        private readonly IPubSubService _pubsub = pubsub;

        public Task HandleAsync(PlayerCoreUpdatedEvent domainEvent, CancellationToken cancellationToken = default) => PublishAsync(domainEvent);
        public Task HandleAsync(AttributeAllocationsChangedEvent domainEvent, CancellationToken cancellationToken = default) => PublishAsync(domainEvent);
        public Task HandleAsync(ItemUnlockedEvent domainEvent, CancellationToken cancellationToken = default) => PublishAsync(domainEvent);
        public Task HandleAsync(ItemEquippedEvent domainEvent, CancellationToken cancellationToken = default) => PublishAsync(domainEvent);
        public Task HandleAsync(ItemUnequippedEvent domainEvent, CancellationToken cancellationToken = default) => PublishAsync(domainEvent);
        public Task HandleAsync(ModUnlockedEvent domainEvent, CancellationToken cancellationToken = default) => PublishAsync(domainEvent);
        public Task HandleAsync(ModAppliedEvent domainEvent, CancellationToken cancellationToken = default) => PublishAsync(domainEvent);
        public Task HandleAsync(ModRemovedEvent domainEvent, CancellationToken cancellationToken = default) => PublishAsync(domainEvent);
        public Task HandleAsync(SkillUnlockedEvent domainEvent, CancellationToken cancellationToken = default) => PublishAsync(domainEvent);
        public Task HandleAsync(SelectedSkillsChangedEvent domainEvent, CancellationToken cancellationToken = default) => PublishAsync(domainEvent);
        public Task HandleAsync(ItemFavoriteChangedEvent domainEvent, CancellationToken cancellationToken = default) => PublishAsync(domainEvent);
        public Task HandleAsync(LogPreferenceChangedEvent domainEvent, CancellationToken cancellationToken = default) => PublishAsync(domainEvent);

        private async Task PublishAsync<T>(T domainEvent) where T : IDomainEvent
        {
            var envelope = new DomainEventEnvelope
            {
                Type = domainEvent.GetType().Name,
                Payload = domainEvent.Serialize(),
            };

            await _pubsub.Publish(Constants.PUBSUB_PLAYER_CHANNEL, Constants.PUBSUB_PLAYER_QUEUE, envelope);
        }
    }
}
