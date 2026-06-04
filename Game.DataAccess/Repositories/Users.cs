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
            return await _context.Users
                .Include(u => u.Players)
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Username == username && u.ArchivedAt == null);
        }

        public async Task<bool> CheckIfUsernameExists(string username)
        {
            return await _context.Users.AnyAsync(u => u.Username == username && u.ArchivedAt == null);
        }

        public async Task<User?> GetUserById(int id)
        {
            return await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<List<User>> SearchUsers(string? search, int? roleId, int skip, int take)
        {
            return await FilteredUsers(search, roleId)
                .Include(u => u.Roles)
                .OrderBy(u => u.Username)
                .ThenBy(u => u.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public Task<int> CountUsers(string? search, int? roleId)
        {
            return FilteredUsers(search, roleId).CountAsync();
        }

        public async Task<bool> SetUserRoles(int userId, IReadOnlyCollection<int> roleIds)
        {
            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
            {
                return false;
            }

            user.Roles.RemoveAll(r => !roleIds.Contains(r.Id));

            var existingRoleIds = user.Roles.Select(r => r.Id).ToList();
            var rolesToAdd = await _context.Roles
                .Where(r => roleIds.Contains(r.Id) && !existingRoleIds.Contains(r.Id))
                .ToListAsync();
            user.Roles.AddRange(rolesToAdd);

            return true;
        }

        public Task<bool> ArchiveUser(int userId)
        {
            return SetUserTimestamp(userId, user => user.ArchivedAt = DateTime.UtcNow);
        }

        public Task<bool> BanUser(int userId)
        {
            return SetUserTimestamp(userId, user => user.BannedAt = DateTime.UtcNow);
        }

        private async Task<bool> SetUserTimestamp(int userId, Action<User> setTimestamp)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
            {
                return false;
            }

            setTimestamp(user);
            return true;
        }

        private IQueryable<User> FilteredUsers(string? search, int? roleId)
        {
            var query = _context.Users.Where(u => u.ArchivedAt == null);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u => EF.Functions.ILike(u.Username, $"%{search}%"));
            }

            if (roleId is not null)
            {
                query = query.Where(u => u.Roles.Any(r => r.Id == roleId));
            }

            return query;
        }
    }
}
