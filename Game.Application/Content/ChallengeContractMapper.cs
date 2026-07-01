using Game.Core;
using Contracts = Game.Abstractions.Contracts;
using CoreChallenge = Game.Core.Progress.Challenge;

namespace Game.Application.Content
{
    /// <summary>
    /// Projects the gameplay domain <see cref="CoreChallenge"/> onto its published read contract. Unlike the
    /// other reference sets, <c>IChallenges.All</c> deliberately serves the rich domain model (see backend.md),
    /// so this flattening — the domain's <c>ChallengeType</c> down to the type id plus the statistic/entity
    /// dimensions it derives — is the single source of truth shared by the <c>GetChallenges</c> socket command
    /// and the content exporter.
    /// </summary>
    public static class ChallengeContractMapper
    {
        public static Contracts.Challenge ToContract(CoreChallenge challenge)
        {
            return new Contracts.Challenge
            {
                Id = challenge.Id,
                Name = challenge.Name,
                Description = challenge.Description,
                ChallengeTypeId = challenge.Type.Id,
                StatisticType = challenge.Type.StatisticType?.Id,
                EntityType = challenge.Type.StatisticType?.EntityType ?? EEntityType.None,
                TargetEntityId = challenge.TargetEntityId,
                ProgressGoal = challenge.ProgressGoal,
                RewardItemId = challenge.RewardItemId,
                RewardItemModId = challenge.RewardItemModId,
                DesignerNotes = challenge.DesignerNotes,
                RetiredAt = challenge.RetiredAt,
            };
        }
    }
}
