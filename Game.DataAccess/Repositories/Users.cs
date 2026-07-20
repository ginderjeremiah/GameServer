using System.Linq.Expressions;
using Game.Abstractions.Contracts.Identity;
using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Identity;
using Game.Core.Players;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using RoleEntity = Game.Infrastructure.Entities.Role;
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
                IsBanned = u.BannedAt != null,
            };

        private static readonly Expression<Func<UserEntity, AccountState>> ToAccountState =
            u => new AccountState
            {
                Id = u.Id,
                Roles = u.Roles.Select(r => r.Name).ToList(),
                IsBanned = u.BannedAt != null,
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
                // Mirrors the GetPlayerSummaries projection below — keep the two in step.
                Players = u.Players.Select(p => new PlayerSummary
                {
                    Id = p.Id,
                    Name = p.Name,
                    Level = p.Level,
                    CurrentZoneId = p.CurrentZoneId,
                    LastActivity = p.LastActivity,
                }).ToList(),
            };

        private readonly GameContext _context;

        public Users(GameContext context)
        {
            _context = context;
        }

        public async Task<bool> CreateAccount(NewAccount account, CancellationToken cancellationToken = default)
        {
            // A new account is created with no characters — its first one is created later, on the select
            // screen. The user carries no cache write or domain events either: nothing is loaded into the
            // cache until login.
            var user = new UserEntity
            {
                Username = account.Username,
                PassHash = account.PassHash,
                LastLogin = DateTime.UtcNow,
            };

            _context.Users.Add(user);

            try
            {
                // Commit here (rather than deferring to the per-request unit of work) so the active-username
                // unique index's rejection of a concurrent duplicate surfaces as a result, not a 500 raised
                // outside the action by the commit filter.
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                // The only unique constraint a new account can violate is the active-username index, so a
                // unique violation means another request created the same active username concurrently.
                // Clear the tracker (it holds only this account's rolled-back insert) so the unit-of-work
                // commit filter doesn't re-attempt it after the action returns.
                _context.ChangeTracker.Clear();
                return false;
            }
        }

        public async Task<CreatePlayerResult> CreatePlayer(int userId, NewPlayer player, int maxPlayersPerAccount, CancellationToken cancellationToken = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // Lock the owning user row so the cap check and the insert it gates can't be split by a
            // concurrent CreatePlayer for the same account — without it two parallel requests could both
            // observe an under-cap count and both insert, exceeding the cap the check exists to enforce.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE", cancellationToken);

            // Resolve the (tracked) user under the lock; an archived account no longer owns characters, so
            // it can't create one. Loading the user also supplies the navigation the mapper links the new
            // player to, mirroring the account-creation path.
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.ArchivedAt == null, cancellationToken);
            if (user is null)
            {
                return CreatePlayerResult.Failed(CreatePlayerOutcome.UserNotFound);
            }

            var playerCount = await _context.Players.CountAsync(p => p.UserId == userId, cancellationToken);
            if (playerCount >= maxPlayersPerAccount)
            {
                return CreatePlayerResult.Failed(CreatePlayerOutcome.CapReached);
            }

            var entity = PlayerMapper.ToEntity(player, user);
            _context.Players.Add(entity);

            try
            {
                // Commit in-tier (like CreateAccount) so the insert is serialized with the cap check under
                // the same lock, rather than deferring to the per-request unit of work that runs after the
                // lock drops.
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                // A player insert has no expected unique-violation path, but an unexpected save failure rolls
                // back the transaction while leaving the Added entity tracked — clear it (mirroring
                // CreateAccount) so the unit-of-work commit filter doesn't re-attempt the insert outside this
                // method's lock after the action returns.
                _context.ChangeTracker.Clear();
                throw;
            }

            return CreatePlayerResult.Created(PlayerMapper.ToSummary(entity));
        }

        public Task<AccountCredentials?> GetUser(string username, CancellationToken cancellationToken = default)
        {
            return _context.Users
                .Where(u => u.Username == username && u.ArchivedAt == null)
                .Select(ToCredentials)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<int>> GetPlayerIds(int userId, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => u.Id == userId && u.ArchivedAt == null)
                .SelectMany(u => u.Players.Select(p => p.Id))
                .ToListAsync(cancellationToken);
        }

        public Task<AccountState?> GetAccountState(int userId, CancellationToken cancellationToken = default)
        {
            return _context.Users
                .Where(u => u.Id == userId && u.ArchivedAt == null)
                .Select(ToAccountState)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PlayerSummary>> GetPlayerSummaries(int userId, CancellationToken cancellationToken = default)
        {
            // Mirrors the ToAdminUser player projection above — keep the two in step.
            return await _context.Users
                .Where(u => u.Id == userId && u.ArchivedAt == null)
                .SelectMany(u => u.Players.Select(p => new PlayerSummary
                {
                    Id = p.Id,
                    Name = p.Name,
                    Level = p.Level,
                    CurrentZoneId = p.CurrentZoneId,
                    LastActivity = p.LastActivity,
                }))
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> CheckIfUsernameExists(string username, CancellationToken cancellationToken = default)
        {
            return await _context.Users.AnyAsync(u => u.Username == username && u.ArchivedAt == null, cancellationToken);
        }

        public Task UpdatePasswordHash(int userId, string passHash, CancellationToken cancellationToken = default)
        {
            // A targeted, immediate update keyed on the id — independent of the per-action unit of work,
            // since the credential upgrade is self-contained and re-applying it converges to the same state.
            return _context.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.PassHash, passHash), cancellationToken);
        }

        public async Task<List<AdminUser>> SearchUsers(string? search, int? roleId, bool? archived, int skip, int take, CancellationToken cancellationToken = default)
        {
            return await FilteredUsers(search, roleId, archived)
                .OrderBy(u => u.Username)
                .ThenBy(u => u.Id)
                .Skip(skip)
                .Take(take)
                .Select(ToAdminUser)
                .ToListAsync(cancellationToken);
        }

        public Task<int> CountUsers(string? search, int? roleId, bool? archived, CancellationToken cancellationToken = default)
        {
            return FilteredUsers(search, roleId, archived).CountAsync(cancellationToken);
        }

        public async Task<SetUserRolesStatus> SetUserRoles(int actingUserId, int targetUserId, IReadOnlyCollection<int> roleIds, CancellationToken cancellationToken = default)
        {
            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);

            if (user is null)
            {
                return SetUserRolesStatus.UserNotFound;
            }

            // Reject the whole assignment if any submitted id is not a real role, validated against the
            // persistence layer's own role set rather than a caller-side check.
            var requestedRoleIds = roleIds.Distinct().ToList();
            var knownRoles = await _context.Roles
                .Where(r => requestedRoleIds.Contains(r.Id))
                .ToListAsync(cancellationToken);

            if (knownRoles.Count != requestedRoleIds.Count)
            {
                return SetUserRolesStatus.UnknownRole;
            }

            // Only an Admin-role removal can trip the lockout rules; a grant or any change to a non-admin
            // carries no hazard and applies on the deferred unit-of-work path. (CheckRoleChange would return
            // Allowed for those regardless of the surviving-admin count, so they skip the serialized count.)
            const int adminRoleId = (int)ERole.Admin;
            var targetHasAdminRole = user.Roles.Any(r => r.Id == adminRoleId);
            var requestedRolesIncludeAdmin = requestedRoleIds.Contains(adminRoleId);
            if (!(targetHasAdminRole && !requestedRolesIncludeAdmin))
            {
                ApplyRequestedRoles(user, requestedRoleIds, knownRoles);
                return SetUserRolesStatus.Success;
            }

            return await RemoveAdminRoleAtomically(
                actingUserId, user, requestedRoleIds, knownRoles, adminRoleId, targetHasAdminRole, requestedRolesIncludeAdmin, cancellationToken);
        }

        /// <summary>
        /// Applies an Admin-role removal as an atomic check-then-act so the last-admin invariant holds under
        /// concurrency. Without it, two simultaneous demotions of different admins could each observe another
        /// admin remaining and both commit, leaving the system with zero admins. A row lock on the Admin role
        /// serializes every admin-membership change through a single point; the surviving-admin count the
        /// domain policy reads is gathered under that lock, and the mutation commits in-tier (like
        /// <see cref="CreateAccount"/>) rather than deferring to the request's unit of work — so the policy's
        /// check and the act it gates can't be split apart.
        /// </summary>
        private async Task<SetUserRolesStatus> RemoveAdminRoleAtomically(
            int actingUserId,
            UserEntity user,
            List<int> requestedRoleIds,
            List<RoleEntity> knownRoles,
            int adminRoleId,
            bool targetHasAdminRole,
            bool requestedRolesIncludeAdmin,
            CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // Lock the Admin role row; the lock is held until commit, serializing concurrent demotions.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Roles\" WHERE \"Id\" = {adminRoleId} FOR UPDATE", cancellationToken);

            // Gather the surviving-admin fact under the lock, then let the domain policy decide. A removal
            // that strands the system with only banned admins is as much a lockout as one that leaves none,
            // so the count uses the same usable-admin definition as the archive/ban guard.
            var otherUsableAdminsRemain = await OtherUsableAdminsRemainAsync(user.Id, cancellationToken);

            var protection = AdminLockoutPolicy.CheckRoleChange(
                actingUserId, user.Id, targetHasAdminRole, requestedRolesIncludeAdmin, otherUsableAdminsRemain);
            switch (protection)
            {
                case RoleChangeProtection.SelfAdminRemoval:
                    return SetUserRolesStatus.SelfAdminRemoval;
                case RoleChangeProtection.LastAdmin:
                    return SetUserRolesStatus.LastAdmin;
            }

            ApplyRequestedRoles(user, requestedRoleIds, knownRoles);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SetUserRolesStatus.Success;
        }

        private static void ApplyRequestedRoles(UserEntity user, List<int> requestedRoleIds, List<RoleEntity> knownRoles)
        {
            user.Roles.RemoveAll(r => !requestedRoleIds.Contains(r.Id));

            var existingRoleIds = user.Roles.Select(r => r.Id).ToHashSet();
            user.Roles.AddRange(knownRoles.Where(r => !existingRoleIds.Contains(r.Id)));
        }

        public Task<UserActionStatus> ArchiveUser(int actingUserId, int targetUserId, CancellationToken cancellationToken = default)
        {
            return SetUserTimestamp(actingUserId, targetUserId, user => user.ArchivedAt = DateTime.UtcNow, cancellationToken);
        }

        public Task<UserActionStatus> BanUser(int actingUserId, int targetUserId, CancellationToken cancellationToken = default)
        {
            return SetUserTimestamp(actingUserId, targetUserId, user => user.BannedAt = DateTime.UtcNow, cancellationToken);
        }

        private async Task<UserActionStatus> SetUserTimestamp(int actingUserId, int targetUserId, Action<UserEntity> setTimestamp, CancellationToken cancellationToken)
        {
            // Cheap self-target rejection before touching the database — an admin can never archive or ban
            // their own account regardless of who else holds the role.
            if (AdminLockoutPolicy.IsSelfTarget(actingUserId, targetUserId))
            {
                return UserActionStatus.SelfTarget;
            }

            var user = await _context.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
            if (user is null)
            {
                return UserActionStatus.UserNotFound;
            }

            // Only acting on an admin can trip the lockout rules; archiving/banning a non-admin carries no
            // hazard and applies on the deferred unit-of-work path.
            const int adminRoleId = (int)ERole.Admin;
            var targetHasAdminRole = user.Roles.Any(r => r.Id == adminRoleId);
            if (!targetHasAdminRole)
            {
                setTimestamp(user);
                return UserActionStatus.Success;
            }

            return await ApplyLifecycleActionToAdminAtomically(actingUserId, user, setTimestamp, adminRoleId, cancellationToken);
        }

        /// <summary>
        /// Applies an archive/ban to an admin as an atomic check-then-act so the last-admin invariant holds
        /// under concurrency. Without it, two simultaneous mutual archives/bans could each observe another
        /// admin remaining and both commit, leaving the system with zero usable admins. A row lock on the
        /// Admin role serializes every admin-membership change (here and in <see cref="SetUserRoles"/>)
        /// through a single point; the surviving-admin fact the domain policy reads is gathered under that
        /// lock, and the mutation commits in-tier (like <see cref="CreateAccount"/>) rather than deferring to
        /// the request's unit of work — so the policy's check and the act it gates can't be split apart.
        /// </summary>
        private async Task<UserActionStatus> ApplyLifecycleActionToAdminAtomically(
            int actingUserId,
            UserEntity user,
            Action<UserEntity> setTimestamp,
            int adminRoleId,
            CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // Lock the Admin role row; the lock is held until commit, serializing concurrent admin changes.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Roles\" WHERE \"Id\" = {adminRoleId} FOR UPDATE", cancellationToken);

            // Gather the surviving-admin fact under the lock, then let the domain policy decide. Both
            // archiving and banning the target take it out of the usable-admin pool, so the policy must
            // reject the action that would empty it.
            var otherUsableAdminsRemain = await OtherUsableAdminsRemainAsync(user.Id, cancellationToken);

            var protection = AdminLockoutPolicy.CheckUserAction(
                actingUserId, user.Id, targetHasAdminRole: true, otherUsableAdminsRemain);
            switch (protection)
            {
                case UserActionProtection.SelfTarget:
                    return UserActionStatus.SelfTarget;
                case UserActionProtection.LastAdmin:
                    return UserActionStatus.LastAdmin;
            }

            setTimestamp(user);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return UserActionStatus.Success;
        }

        public async Task<UserActionStatus> UnarchiveUser(int actingUserId, int targetUserId, CancellationToken cancellationToken = default)
        {
            if (AdminLockoutPolicy.IsSelfTarget(actingUserId, targetUserId))
            {
                return UserActionStatus.SelfTarget;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
            if (user is null)
            {
                return UserActionStatus.UserNotFound;
            }

            if (user.ArchivedAt is null)
            {
                return UserActionStatus.Success;
            }

            // Archiving frees the username, so another active account may have claimed it since. Check up
            // front for the common case, then still guard the save itself (below) against the rarer race of
            // a concurrent claim landing between this check and the commit.
            var usernameTaken = await _context.Users.AnyAsync(
                u => u.Id != targetUserId && u.Username == user.Username && u.ArchivedAt == null, cancellationToken);
            if (usernameTaken)
            {
                return UserActionStatus.UsernameTaken;
            }

            user.ArchivedAt = null;

            try
            {
                // Commit here (like CreateAccount) rather than deferring to the per-request unit of work, so
                // a concurrent claim of this username that slips past the check above still surfaces as
                // UsernameTaken instead of an unhandled 500 from the deferred commit filter.
                await _context.SaveChangesAsync(cancellationToken);
                return UserActionStatus.Success;
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                _context.ChangeTracker.Clear();
                return UserActionStatus.UsernameTaken;
            }
        }

        public Task<UserActionStatus> UnbanUser(int actingUserId, int targetUserId, CancellationToken cancellationToken = default)
        {
            return ClearUserTimestamp(actingUserId, targetUserId, user => user.BannedAt = null, cancellationToken);
        }

        private async Task<UserActionStatus> ClearUserTimestamp(int actingUserId, int targetUserId, Action<UserEntity> clearTimestamp, CancellationToken cancellationToken)
        {
            // Reinstating never reduces the usable-admin pool, so unlike archive/ban this only needs the
            // self-target guard — not the last-admin check — but still needs it: without it, a banned/archived
            // admin's still-valid session token (bans/archives are enforced at login, not re-checked per
            // request) would let them undo the action against themselves before the token expires.
            if (AdminLockoutPolicy.IsSelfTarget(actingUserId, targetUserId))
            {
                return UserActionStatus.SelfTarget;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken);
            if (user is null)
            {
                return UserActionStatus.UserNotFound;
            }

            clearTimestamp(user);
            return UserActionStatus.Success;
        }

        /// <summary>
        /// Whether any usable admin other than <paramref name="excludingUserId"/> remains. A usable admin
        /// can still log in to recover the instance, so it must be neither archived nor banned. Shared by
        /// both admin-lockout guards (role removal and archive/ban) so they enforce one definition of the
        /// surviving-admin pool. Call within the Admin-role lock so the read is serialized with the mutation.
        /// </summary>
        private Task<bool> OtherUsableAdminsRemainAsync(int excludingUserId, CancellationToken cancellationToken)
        {
            const int adminRoleId = (int)ERole.Admin;
            return _context.Users.AnyAsync(u =>
                u.Id != excludingUserId
                && u.ArchivedAt == null
                && u.BannedAt == null
                && u.Roles.Any(r => r.Id == adminRoleId), cancellationToken);
        }

        private const string LikeEscapeCharacter = "\\";

        // Escapes ILIKE's own wildcard characters so a search term is matched literally, e.g. a
        // username containing "%" or "_" is only findable by that literal text, not as a wildcard.
        private static string EscapeLikePattern(string search) =>
            search
                .Replace(LikeEscapeCharacter, LikeEscapeCharacter + LikeEscapeCharacter)
                .Replace("%", LikeEscapeCharacter + "%")
                .Replace("_", LikeEscapeCharacter + "_");

        private IQueryable<UserEntity> FilteredUsers(string? search, int? roleId, bool? archived)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var pattern = $"%{EscapeLikePattern(search)}%";
                query = query.Where(u =>
                    EF.Functions.ILike(u.Username, pattern, LikeEscapeCharacter)
                    || u.Players.Any(p => EF.Functions.ILike(p.Name, pattern, LikeEscapeCharacter)));
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
