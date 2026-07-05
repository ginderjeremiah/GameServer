using Game.Core;
using Game.Core.Players;
using Game.DataAccess.Mapping;
using Microsoft.EntityFrameworkCore;
using EntityPlayer = Game.Infrastructure.Entities.Player;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// The single sanctioned way to read a player for rehydration. It projects straight into the lean
    /// <see cref="PlayerCacheModel"/> — pulling only the player's own relational columns and reducing reference
    /// data (item/skill/mod definitions) to ids, which are re-resolved from the in-memory catalogs in
    /// <see cref="PlayerCacheMapper.ToCore"/>. Because the model's members are <c>required</c>, the projection
    /// cannot silently omit one (a compile error), so completeness is structural rather than a convention. The
    /// flat, relational shape keeps the projection a trivial column copy with no correlated sub-queries.
    /// <para>
    /// It is split into one query per collection (<see cref="RelationalQueryableExtensions.AsSplitQuery{TEntity}"/>):
    /// the unbounded collections (unlocked items, applied mods, skills) would otherwise be JOINed into a single
    /// result set, multiplying into a cartesian product that grows as the player progresses.
    /// </para>
    /// </summary>
    internal static class PlayerQueryExtensions
    {
        public static IQueryable<PlayerCacheModel> SelectPlayerCacheModel(this IQueryable<EntityPlayer> players)
        {
            return players.Select(p => new PlayerCacheModel
            {
                Id = p.Id,
                ClassId = p.ClassId,
                Name = p.Name,
                Level = p.Level,
                Exp = p.Exp,
                CurrentZoneId = p.CurrentZoneId,
                LastActivity = p.LastActivity,
                AutoChallengeBoss = p.AutoChallengeBoss,
                StatPointsGained = p.StatPointsGained,
                StatPointsUsed = p.StatPointsUsed,
                StatAllocations = p.PlayerAttributes
                    .Select(pa => new StatAllocation { Attribute = (EAttribute)pa.AttributeId, Amount = (double)pa.Amount })
                    .ToList(),
                UnlockedItems = p.UnlockedItems
                    .Select(ui => new CachedUnlockedItem { ItemId = ui.ItemId, EquipmentSlotId = ui.EquipmentSlotId, Favorite = ui.Favorite })
                    .ToList(),
                AppliedMods = p.AppliedMods
                    .Select(am => new CachedAppliedMod { ItemId = am.ItemId, ItemModId = am.ItemModId, ItemModSlotId = am.ItemModSlotId })
                    .ToList(),
                UnlockedModIds = p.UnlockedMods.Select(um => um.ItemModId).ToList(),
                Skills = p.PlayerSkills
                    .Select(ps => new CachedPlayerSkill { SkillId = ps.SkillId, Selected = ps.Selected, Order = ps.Order })
                    .ToList(),
                LogPreferences = p.LogPreferences
                    .Select(lp => new LogPreference { LogType = (ELogType)lp.LogTypeId, Enabled = lp.Enabled })
                    .ToList(),
                Lessons = p.PlayerLessons
                    .Select(pl => new PlayerLesson { LessonId = pl.LessonId, UnlockedAt = pl.UnlockedAt, ReadAt = pl.ReadAt })
                    .ToList(),
            }).AsSplitQuery();
        }
    }
}
