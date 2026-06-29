using Game.Core;
using Contracts = Game.Abstractions.Contracts;
using EntityPath = Game.Infrastructure.Entities.Path;

namespace Game.DataAccess.Mapping
{
    internal static class PathMapper
    {
        /// <summary>Maps an entity <see cref="EntityPath"/> to the read/authoring contract.</summary>
        public static Contracts.Path ToContract(EntityPath entity)
        {
            return new Contracts.Path
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                ActivityKey = (EActivityKey)entity.ActivityKey,
                RetiredAt = entity.RetiredAt,
            };
        }
    }
}
