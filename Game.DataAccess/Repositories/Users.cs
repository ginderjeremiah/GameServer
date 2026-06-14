using System.Linq.Expressions;
using Game.Abstractions.Contracts.Identity;
using Game.Abstractions.DataAccess;
using Game.Core.Players;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using UserEntity = Game.Infrastructure.Entities.User;

namespace Game.DataAccess.Repositories
{
    internal class Users : IUsers
    {
        // Single source of truth for the entity -> read-contract projections so EF translates them in
        // SQL. The entity stays an implementation detail of the data tier; callers see only contracts.
        private static readonly Expression<Func<UserEntity, AccountCredentials>> ToCredentials =
            u => new AccountCredentials
            {
                Id = u.Id,
                PassHash = u.PassHash,
                Roles = u.Roles.Select(r => r.Name).ToList(),
                PlayerIds = u.Players.Select(p => p.Id).ToList(),
            };

        private static readonly Expression<Func<UserEntity, AdminUser>> ToAdminUser =
            u => new AdminUser
            {
                Id = u.Id,
                Username = u.Username,
                LastLogin = u.LastLogin,
                ArchivedAt = u.ArchivedAt,
                BannedAt = u.BannedAt,
                Roles = u.Roles.Select(r => new Role { Id = r.Id, Name = r.Name }).ToList(),
                Players = u.Players.Select(p => new PlayerSummary
                {
                    Id = p.Id,
                    Name = p.Name,
                    Level = p.Level,
                    LastActivity = p.LastActivity,
                }).ToList(),
            };

        private readonly GameContext _context;

        public Users(GameContext context)
        {
            _context = context;
        }

        public void CreateAccount(NewAccount account, NewPlayer player)
        {
            // The account graph is persisted straight through the unit of work (no cache write or domain
            // events): a freshly created player is only loaded into the cache later, on login. The player
            // links to the user via navigation, so EF resolves the FK without the store-generated user id.
            var user = new UserEntity
            {
                Username = account.Username,
                PassHash = account.PassHash,
                LastLogin = DateTime.UtcNow,
            };

            _context.Users.Add(user);
            _context.Players.Add(PlayerMapper.ToEntity(player, user));
        }

        public Task<AccountCredentials?> GetUser(string username)
        {
            return _context.Users
                .Where(u => u.Username == username && u.ArchivedAt == null)
                .Select(ToCredentials)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> CheckIfUsernameExists(string username)
        {
            return await _context.Users.AnyAsync(u => u.Username == username && u.ArchivedAt == null);
        }

        public Task UpdatePasswordHash(int userId, string passHash)
        {
            // A targeted, immediate update keyed on the id — independent of the per-action unit of work,
            // since the credential upgrade is self-contained and re-applying it converges to the same state.
            return _context.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.PassHash, passHash));
        }

        public async Task<List<AdminUser>> SearchUsers(string? search, int? roleId, bool? archived, int skip, int take)
        {
            return await FilteredUsers(search, roleId, archived)
                .OrderBy(u => u.Username)
                .ThenBy(u => u.Id)
                .Skip(skip)
                .Take(take)
                .Select(ToAdminUser)
                .ToListAsync();
        }

        public Task<int> CountUsers(string? search, int? roleId, bool? archived)
        {
            return FilteredUsers(search, roleId, archived).CountAsync();
        }

        public async Task<SetUserRolesStatus> SetUserRoles(int userId, IReadOnlyCollection<int> roleIds)
        {
            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
            {
                return SetUserRolesStatus.UserNotFound;
            }

            // Reject the whole assignment if any submitted id is not a real role, validated against the
            // persistence layer's own role set rather than a caller-side check.
            var requestedRoleIds = roleIds.Distinct().ToList();
            var knownRoles = await _context.Roles
                .Where(r => requestedRoleIds.Contains(r.Id))
                .ToListAsync();

            if (knownRoles.Count != requestedRoleIds.Count)
            {
                return SetUserRolesStatus.UnknownRole;
            }

            user.Roles.RemoveAll(r => !requestedRoleIds.Contains(r.Id));

            var existingRoleIds = user.Roles.Select(r => r.Id).ToHashSet();
            user.Roles.AddRange(knownRoles.Where(r => !existingRoleIds.Contains(r.Id)));

            return SetUserRolesStatus.Success;
        }

        public Task<bool> ArchiveUser(int userId)
        {
            return SetUserTimestamp(userId, user => user.ArchivedAt = DateTime.UtcNow);
        }

        public Task<bool> BanUser(int userId)
        {
            return SetUserTimestamp(userId, user => user.BannedAt = DateTime.UtcNow);
        }

        private async Task<bool> SetUserTimestamp(int userId, Action<UserEntity> setTimestamp)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
            {
                return false;
            }

            setTimestamp(user);
            return true;
        }

        private IQueryable<UserEntity> FilteredUsers(string? search, int? roleId, bool? archived)
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
