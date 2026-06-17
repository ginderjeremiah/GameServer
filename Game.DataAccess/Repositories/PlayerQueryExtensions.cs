using Microsoft.EntityFrameworkCore;
using EntityPlayer = Game.Infrastructure.Entities.Player;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// The single sanctioned way to materialize a player entity for <see cref="Mapping.PlayerMapper.ToCore"/>.
    /// <see cref="Mapping.PlayerMapper.ToCore"/> reads every navigation collection applied below; loading a
    /// player without them would silently map an empty graph (no skills/items/preferences) since lazy loading
    /// is off. Centralizing the includes here makes that contract structural — a new load path that routes
    /// through this extension cannot accidentally omit one — rather than a comment on each query.
    /// </summary>
    internal static class PlayerQueryExtensions
    {
        /// <summary>
        /// Applies the full include graph that <see cref="Mapping.PlayerMapper.ToCore"/> requires. The
        /// reference-data portion (item/skill/mod definitions) is resolved from the in-memory cached catalogs
        /// in the mapper, so only the player-specific relational data is included here.
        /// </summary>
        public static IQueryable<EntityPlayer> IncludePlayerGraph(this IQueryable<EntityPlayer> players)
        {
            return players
                .Include(p => p.PlayerAttributes)
                .Include(p => p.PlayerSkills)
                .Include(p => p.UnlockedItems)
                .Include(p => p.UnlockedMods)
                .Include(p => p.AppliedMods)
                .Include(p => p.LogPreferences)
                .AsSplitQuery();
        }
    }
}
