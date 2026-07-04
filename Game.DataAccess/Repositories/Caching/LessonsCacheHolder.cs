using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// An immutable snapshot of the lesson reference set (#1591, spike #1392): the ordered lesson entity list
    /// (each with its steps loaded), used for both the contract projection and the admin entity lookups.
    /// </summary>
    internal sealed record LessonSnapshot(IReadOnlyList<Lesson> Entities);

    /// <summary>Singleton snapshot holder for the cached lesson entity list.</summary>
    internal sealed class LessonsCacheHolder(IServiceScopeFactory scopeFactory) : ReferenceCacheHolder<LessonSnapshot>(scopeFactory)
    {
        protected override async Task<LessonSnapshot> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
        {
            var entities = await context.Lessons
                .AsNoTracking()
                .Include(l => l.Steps)
                .AsSplitQuery()
                .OrderBy(l => l.Id)
                .ToListAsync(cancellationToken);

            entities.AssertZeroBasedContiguity("Lessons");

            return new LessonSnapshot(entities);
        }
    }
}
