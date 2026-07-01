using Contracts = Game.Abstractions.Contracts;
using EntityChallenge = Game.Infrastructure.Entities.Challenge;

namespace Game.DataAccess.Mapping
{
    /// <summary>
    /// Maps a challenge read contract back to its entity for the content seeder. There is no entity→contract
    /// direction here: reads project the domain challenge (with its derived StatisticType/EntityType) through
    /// <c>ChallengeContractMapper</c> in the application layer, which cannot see EF entities. The derived
    /// <see cref="Contracts.Challenge.StatisticType"/>/<see cref="Contracts.Challenge.EntityType"/> are read-only
    /// projections of <see cref="Contracts.Challenge.ChallengeTypeId"/> and are not persisted.
    /// </summary>
    internal static class ChallengeMapper
    {
        public static EntityChallenge ToEntity(Contracts.Challenge contract)
        {
            return new EntityChallenge
            {
                Id = contract.Id,
                Name = contract.Name,
                Description = contract.Description,
                ChallengeTypeId = (int)contract.ChallengeTypeId,
                TargetEntityId = contract.TargetEntityId,
                ProgressGoal = contract.ProgressGoal,
                RewardItemId = contract.RewardItemId,
                RewardItemModId = contract.RewardItemModId,
                DesignerNotes = contract.DesignerNotes,
                RetiredAt = contract.RetiredAt,
            };
        }
    }
}
