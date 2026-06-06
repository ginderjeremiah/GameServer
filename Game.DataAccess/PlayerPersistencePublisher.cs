using Game.Abstractions.Infrastructure;
using Game.Core.Events;
using Game.Core.Players.Events;
using System.Text.Json;

namespace Game.DataAccess
{
    internal class PlayerPersistencePublisher(IPubSubService pubsub) : IDomainEventHandler<IPlayerPersistenceEvent>
    {
        private readonly IPubSubService _pubsub = pubsub;

        public async Task HandleAsync(IPlayerPersistenceEvent domainEvent, CancellationToken cancellationToken = default)
        {
            var envelope = new DomainEventEnvelope
            {
                Type = domainEvent.GetType().Name,
                Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            };

            await _pubsub.Publish(Constants.PUBSUB_PLAYER_CHANNEL, Constants.PUBSUB_PLAYER_QUEUE, envelope);
        }
    }
}
