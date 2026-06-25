using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreClass = Game.Core.Classes.Class;

namespace Game.DataAccess.Repositories.Caching
{
    /// <summary>
    /// An immutable snapshot of the class reference set: the ordered class entity list and the
    /// level-independent pre-mapped <see cref="CoreClass"/> models the gameplay reads share (aligned by
    /// zero-based id with the entity list). Both are built and published together so a reader can never
    /// observe a new entity list against stale core models.
    /// </summary>
    internal sealed record ClassSnapshot(
        IReadOnlyList<Class> Entities,
        IReadOnlyList<CoreClass> CoreClasses);

    /// <summary>Singleton snapshot holder for the cached class catalogue.</summary>
    internal sealed class ClassesCacheHolder(IServiceScopeFactory scopeFactory)
        : ReferenceCacheHolder<ClassSnapshot>(scopeFactory)
    {
        protected override async Task<ClassSnapshot> BuildSnapshotAsync(GameContext context, CancellationToken cancellationToken)
        {
            var classes = await context.Classes
                .AsNoTracking()
                .Include(c => c.StarterSkills)
                .Include(c => c.StarterEquipment)
                .Include(c => c.AttributeDistributions)
                .AsSplitQuery()
                .OrderBy(c => c.Id)
                .ToListAsync(cancellationToken);

            // The core models align with the entity list by zero-based id; a gap would mis-resolve an index
            // lookup, so assert contiguity before building so a bad reload fails the build-then-swap.
            classes.AssertZeroBasedContiguity("Classes");

            var coreClasses = classes.Select(ClassMapper.ToCore).ToList();

            return new ClassSnapshot(classes, coreClasses);
        }
    }
}
