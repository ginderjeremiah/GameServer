using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class Users : IUsers
    {
        private readonly GameContext _context;

        public Users(GameContext context)
        {
            _context = context;
        }

        public async Task<User?> GetUser(string username)
        {
            return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<bool> CheckIfUsernameExists(string username)
        {
            return await _context.Users.AnyAsync(u => u.Username == username);
        }
    }
}
