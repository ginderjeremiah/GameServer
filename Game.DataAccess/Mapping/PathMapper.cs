using Contracts = Game.Abstractions.Contracts;
using EntityPath = Game.Infrastructure.Entities.Path;

namespace Game.DataAccess.Mapping
{
    internal static class PathMapper
    {
        /// <summary>Maps an entity <see cref="EntityPath"/> (with its skill contributions loaded) to the
        /// read/authoring contract.</summary>
        public static Contracts.Path ToContract(EntityPath entity)
        {
            return new Contracts.Path
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                FalloffBase = entity.FalloffBase,
                RetiredAt = entity.RetiredAt,
                Contributions = entity.SkillContributions
                    .Select(c => new Contracts.SkillPathContribution
                    {
                        SkillId = c.SkillId,
                        HomeTier = c.HomeTier,
                        Weight = c.Weight,
                    }).ToList(),
            };
        }
    }
}
