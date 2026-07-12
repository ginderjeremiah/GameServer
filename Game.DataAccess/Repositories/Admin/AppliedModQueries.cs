using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories.Admin
{
    internal class AppliedModQueries(GameContext context) : IAppliedModQueries
    {
        private readonly GameContext _context = context;

        public IAsyncEnumerable<int> GetOccupiedSlotIds(IReadOnlyCollection<int> slotIds)
        {
            return _context.AppliedMods
                .Where(am => slotIds.Contains(am.ItemModSlotId))
                .Select(am => am.ItemModSlotId)
                .Distinct()
                .AsAsyncEnumerable();
        }
    }
}
