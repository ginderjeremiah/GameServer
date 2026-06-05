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

        public async Task<List<User>> SearchUsers(string? search, int? roleId, bool? archived, int skip, int take)
        {
            return await FilteredUsers(search, roleId, archived)
                .Include(u => u.Players)
                .Include(u => u.Roles)
                .OrderBy(u => u.Username)
                .ThenBy(u => u.Id)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public Task<int> CountUsers(string? search, int? roleId, bool? archived)
        {
            return FilteredUsers(search, roleId, archived).CountAsync();
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

        private IQueryable<User> FilteredUsers(string? search, int? roleId, bool? archived)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{search}%";
                query = query.Where(u =>
                    EF.Functions.ILike(u.Username, pattern)
                    || u.Players.Any(p => EF.Functions.ILike(p.Name, pattern)));
            }

            if (roleId is not null)
            {
                query = query.Where(u => u.Roles.Any(r => r.Id == roleId));
            }

            if (archived is not null)
            {
                query = archived.Value
                    ? query.Where(u => u.ArchivedAt != null)
                    : query.Where(u => u.ArchivedAt == null);
            }

            return query;
        }
    }
}
