using Contracts = Game.Abstractions.Contracts;
using EntityTag = Game.Infrastructure.Entities.Tag;

namespace Game.DataAccess.Mapping
{
    internal static class TagMapper
    {
        /// <summary>Maps a read/authoring <see cref="Contracts.Tag"/> back to its entity for the content seeder.
        /// Tags carry their own non-zero-based identity and are referenced by the item/mod tag-join rows, so they
        /// are seeded ahead of items and mods. The referenced <c>TagCategory</c> is intrinsic (migration-seeded).</summary>
        public static EntityTag ToEntity(Contracts.Tag contract)
        {
            return new EntityTag
            {
                Id = contract.Id,
                Name = contract.Name,
                TagCategoryId = contract.TagCategoryId,
            };
        }
    }
}
