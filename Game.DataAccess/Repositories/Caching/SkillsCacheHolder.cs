using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>Singleton snapshot holder for the cached skill entity list.</summary>
    internal sealed class SkillsCacheHolder(IServiceScopeFactory scopeFactory)
        : ReferenceCacheHolder<IReadOnlyList<Skill>>(scopeFactory)
    {
        protected override async Task<IReadOnlyList<Skill>> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
        {
            return await context.Skills
                .AsNoTracking()
                .Include(s => s.SkillDamageMultipliers)
                .OrderBy(s => s.Id)
                .ToListAsync(cancellationToken);
        }
    }
}
