using Game.Abstractions.Contracts.Identity;
using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Game.Api.Models.Users;
using Game.Api.Services;
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
    /// <see cref="ReloadReferenceCachesAttribute"/>: user accounts are not list-cached in memory, so there
    /// is no read-cache to reload after a write.
    /// </remarks>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    public class AdminUsersController(IUsers users, IRoles roles, SessionService session) : ControllerBase
    {
        private const int MaxPageSize = 100;
        private const int DefaultPageSize = 25;

        private readonly IUsers _users = users;
        private readonly IRoles _roles = roles;
        // Identifies the acting admin (from the validated token) so destructive actions can self-protect.
        private readonly SessionService _session = session;

        [HttpGet]
        public async Task<ApiResponse<AdminUserSearchResults>> GetUsers(
            string? search = null,
            int? roleId = null,
            bool? archived = null,
            int page = 1,
            int pageSize = DefaultPageSize)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

            var totalCount = await _users.CountUsers(search, roleId, archived, HttpContext.RequestAborted);
            var matches = await _users.SearchUsers(search, roleId, archived, (page - 1) * pageSize, pageSize, HttpContext.RequestAborted);

            return ApiResponse.Success(new AdminUserSearchResults
            {
                Users = matches,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            });
        }

        [HttpGet]
        public ApiEnumerableResponse<Role> GetRoles()
        {
            return ApiResponse.Success(_roles.GetRoles());
        }

        [HttpPost]
        public async Task<ApiResponse> SetUserRoles([FromBody] SetUserRolesData data)
        {
            var status = await _users.SetUserRoles(_session.UserId, data.UserId, data.RoleIds, HttpContext.RequestAborted);
            return status switch
            {
                SetUserRolesStatus.Success => ApiResponse.Success(),
                SetUserRolesStatus.UserNotFound => ApiResponse.Error("User not found."),
                SetUserRolesStatus.UnknownRole => ApiResponse.Error("One or more roles do not exist."),
                SetUserRolesStatus.SelfAdminRemoval => ApiResponse.Error("You cannot remove your own Admin role."),
                SetUserRolesStatus.LastAdmin => ApiResponse.Error("Cannot remove the Admin role from the last remaining admin."),
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
            };
        }

        [HttpPost]
        public async Task<ApiResponse> ArchiveUser([FromBody] UserActionData data)
        {
            var status = await _users.ArchiveUser(_session.UserId, data.UserId, HttpContext.RequestAborted);
            return MapUserAction(status, "You cannot archive your own account.", "Cannot archive the last remaining admin.");
        }

        [HttpPost]
        public async Task<ApiResponse> BanUser([FromBody] UserActionData data)
        {
            var status = await _users.BanUser(_session.UserId, data.UserId, HttpContext.RequestAborted);
            return MapUserAction(status, "You cannot ban your own account.", "Cannot ban the last remaining admin.");
        }

        private static ApiResponse MapUserAction(UserActionStatus status, string selfTargetMessage, string lastAdminMessage)
        {
            return status switch
            {
                UserActionStatus.Success => ApiResponse.Success(),
                UserActionStatus.UserNotFound => ApiResponse.Error("User not found."),
                UserActionStatus.SelfTarget => ApiResponse.Error(selfTargetMessage),
                UserActionStatus.LastAdmin => ApiResponse.Error(lastAdminMessage),
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
            };
        }
    }
}
