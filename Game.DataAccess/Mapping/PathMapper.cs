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
                DesignerNotes = entity.DesignerNotes,
                RetiredAt = entity.RetiredAt,
            };
        }

        /// <summary>Maps a read/authoring <see cref="Contracts.Path"/> back to its entity for the content
        /// seeder. Its tiers (proficiencies) carry the path id and are seeded from their own set.</summary>
        public static EntityPath ToEntity(Contracts.Path contract)
        {
            return new EntityPath
            {
                Id = contract.Id,
                Name = contract.Name,
                Description = contract.Description,
                ActivityKey = (int)contract.ActivityKey,
                DesignerNotes = contract.DesignerNotes,
                RetiredAt = contract.RetiredAt,
            };
        }
    }
}
