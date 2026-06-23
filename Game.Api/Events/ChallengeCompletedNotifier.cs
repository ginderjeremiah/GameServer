using Game.Api.Models.Progress;
using Game.Api.Services;
using Game.Api.Sockets.Commands;
using Game.Core.Events;
using Game.Core.Players.Events;

namespace Game.Api.Events
{
    /// <summary>
    /// Bridges the domain <see cref="ChallengeCompletedEvent"/> to the player's live socket so a
    /// challenge's rewards become usable the moment it completes, rather than only after a refresh.
    /// Emitting through <see cref="SocketManagerService"/> routes over the Redis backplane, so it
    /// reaches the player regardless of which instance currently holds their connection.
    /// </summary>
    internal class ChallengeCompletedNotifier(SocketManagerService socketManager)
        : IDomainEventHandler<ChallengeCompletedEvent>
    {
        private readonly SocketManagerService _socketManager = socketManager;

        public Task HandleAsync(ChallengeCompletedEvent domainEvent, CancellationToken cancellationToken = default)
        {
            var model = new ChallengeCompletedModel
            {
                ChallengeId = domainEvent.ChallengeId,
                RewardItemId = domainEvent.RewardItemId,
                RewardItemModId = domainEvent.RewardItemModId,
            };

            return _socketManager.EmitSocketCommand(new ChallengeCompletedInfo(model), domainEvent.PlayerId);
        }
    }
}
