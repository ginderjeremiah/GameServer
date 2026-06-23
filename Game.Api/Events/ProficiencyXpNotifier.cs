using Game.Api.Models.Progress;
using Game.Api.Services;
using Game.Api.Sockets.Commands;
using Game.Core.Events;
using Game.Core.Players.Events;

namespace Game.Api.Events
{
    /// <summary>
    /// Bridges the domain <see cref="ProficiencyXpGainedEvent"/> to the player's live socket so a proficiency
    /// level-up or milestone surfaces the moment a battle is won, rather than only after a refresh. Emitting
    /// through <see cref="SocketManagerService"/> routes over the Redis backplane, so it reaches the player
    /// regardless of which instance currently holds their connection — mirroring
    /// <see cref="ChallengeCompletedNotifier"/>.
    /// </summary>
    internal class ProficiencyXpNotifier(SocketManagerService socketManager)
        : IDomainEventHandler<ProficiencyXpGainedEvent>
    {
        private readonly SocketManagerService _socketManager = socketManager;

        public Task HandleAsync(ProficiencyXpGainedEvent domainEvent, CancellationToken cancellationToken = default)
        {
            var model = new ProficiencyXpGainedModel
            {
                Proficiencies = domainEvent.Results
                    .Select(result => new ProficiencyXpResultModel
                    {
                        ProficiencyId = result.ProficiencyId,
                        XpGained = result.XpGained,
                        NewLevel = result.NewLevel,
                        NewXp = result.NewXp,
                        MilestonesCrossed = [.. result.MilestonesCrossed],
                        GrantedSkillIds = [.. result.GrantedSkillIds],
                    })
                    .ToList(),
                Opened = domainEvent.Opened
                    .Select(opened => new ProficiencyOpenedModel
                    {
                        ProficiencyId = opened.ProficiencyId,
                        SeedSkillId = opened.SeedSkillId,
                    })
                    .ToList(),
            };

            return _socketManager.EmitSocketCommand(new ProficiencyXpGainedInfo(model), domainEvent.PlayerId);
        }
    }
}
