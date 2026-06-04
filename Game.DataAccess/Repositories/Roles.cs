using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class Roles : IRoles
    {
        private readonly GameContext _context;

        public Roles(GameContext context)
        {
            _context = context;
        }

        public Task<List<Role>> GetRoles()
        {
            return _context.Roles
                .AsNoTracking()
                .OrderBy(r => r.Id)
                .ToListAsync();
        }
    }
}
