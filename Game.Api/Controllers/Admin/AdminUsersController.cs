using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Game.Api.Models.Users;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin endpoints for managing user accounts: searching the roster, listing roles, updating a
    /// user's roles, and archiving (soft-deleting) or banning a user. Like the other admin controllers
    /// these share the <c>/api/AdminTools/*</c> route prefix and require the <c>Admin</c> role.
    /// </summary>
    /// <remarks>
    /// Unlike the reference-data admin controllers, this one does not carry
    /// <see cref="AdminCacheInvalidationFilter"/>: user accounts are not list-cached in memory, so there
    /// is no read-cache to invalidate after a write.
    /// </remarks>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    public class AdminUsersController(IUsers users, IRoles roles) : ControllerBase
    {
        private const int MaxPageSize = 100;
        private const int DefaultPageSize = 25;

        private readonly IUsers _users = users;
        private readonly IRoles _roles = roles;

        [HttpGet]
        public async Task<ApiResponse<AdminUserSearchResults>> GetUsers(
            string? search = null,
            int? roleId = null,
            int page = 1,
            int pageSize = DefaultPageSize)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var totalCount = await _users.CountUsers(search, roleId);
            var matches = await _users.SearchUsers(search, roleId, (page - 1) * pageSize, pageSize);

            return ApiResponse.Success(new AdminUserSearchResults
            {
                Users = matches.Select(AdminUser.FromSource).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            });
        }

        [HttpGet]
        public async Task<ApiEnumerableResponse<Role>> GetRoles()
        {
            var allRoles = await _roles.GetRoles();
            return ApiResponse.Success(allRoles.Select(Role.FromSource).ToList());
        }

        [HttpPost]
        public async Task<ApiResponse> SetUserRoles([FromBody] SetUserRolesData data)
        {
            var roleIds = data.RoleIds.Distinct().ToList();
            var knownRoleIds = (await _roles.GetRoles()).Select(r => r.Id).ToHashSet();
            if (roleIds.Any(id => !knownRoleIds.Contains(id)))
            {
                return ApiResponse.Error("One or more roles do not exist.");
            }

            var updated = await _users.SetUserRoles(data.UserId, roleIds);
            return updated ? ApiResponse.Success() : ApiResponse.Error("User not found.");
        }

        [HttpPost]
        public async Task<ApiResponse> ArchiveUser([FromBody] UserActionData data)
        {
            var archived = await _users.ArchiveUser(data.UserId);
            return archived ? ApiResponse.Success() : ApiResponse.Error("User not found.");
        }

        [HttpPost]
        public async Task<ApiResponse> BanUser([FromBody] UserActionData data)
        {
            var banned = await _users.BanUser(data.UserId);
            return banned ? ApiResponse.Success() : ApiResponse.Error("User not found.");
        }
    }
}
