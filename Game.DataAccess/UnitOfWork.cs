using Game.Application;
using Game.Infrastructure.Database;

namespace Game.DataAccess
{
    internal class UnitOfWork(GameContext context) : IUnitOfWork
    {
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return context.SaveChangesAsync(cancellationToken);
        }
    }
}
