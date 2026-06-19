using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Api.Models.Users;
using Game.Core;
using Game.Infrastructure.Database;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using ApiRole = Game.Abstractions.Contracts.Identity.Role;

namespace Game.Api.Tests.Integration
{
    [Collection("Integration")]
    public class AdminUsersControllerTests : ApiIntegrationTestBase
    {
        public AdminUsersControllerTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper) : base(containers, testOutputHelper) { }

        /// <summary>
        /// Creates an admin account (with a player so a session can be established) and returns an
        /// authenticated client whose token carries the requested role.
        /// </summary>
        private async Task<HttpClient> SetupAdminClientAsync(bool admin = true)
        {
            var (client, _) = await SetupAdminClientWithIdAsync(admin);
            return client;
        }

        /// <summary>
        /// Like <see cref="SetupAdminClientAsync"/> but also returns the acting admin's user id. When the
        /// caller is an admin the Admin role is granted in the database (not just the token) so the
        /// self-protection rules — which read the persisted roles — see the caller as a real admin and
        /// self-targeting / last-admin scenarios can be exercised.
        /// </summary>
        private async Task<(HttpClient Client, int UserId)> SetupAdminClientWithIdAsync(bool admin = true)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var user = await TestDataSeeder.CreateUserAsync(context, "adminuser", "adminpass");
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id);

            var roles = admin ? new[] { nameof(ERole.Admin) } : [];
            if (admin)
            {
                await TestDataSeeder.AssignRoleToUserAsync(context, user.Id, ERole.Admin);
            }

            return (CreateAuthenticatedClient(user.Id, player.Id, roles), user.Id);
        }

        private async Task SeedDefaultSkillsAsync()
        {
            // CreateAccount provisions a new player with PlayerSkills referencing SkillId 0, 1, 2.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            await TestDataSeeder.CreateSkillAsync(context, "Skill0");
            await TestDataSeeder.CreateSkillAsync(context, "Skill1");
            await TestDataSeeder.CreateSkillAsync(context, "Skill2");
        }

        private static async Task<AdminUserSearchResults> GetUsersAsync(HttpClient client, string query = "")
        {
            var response = await client.GetAsync($"/api/AdminTools/GetUsers{query}", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<AdminUserSearchResults>>(CancellationToken);
            Assert.NotNull(result?.Data);
            return result.Data;
        }

        [Fact]
        public async Task GetUsers_ReturnsAllUsersWithPaging()
        {
            using var authClient = await SetupAdminClientAsync();
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.CreateUserAsync(context, "alice", "pw");
                await TestDataSeeder.CreateUserAsync(context, "bob", "pw");
                await TestDataSeeder.CreateUserAsync(context, "carol", "pw");
            }

            // Four users total (adminuser, alice, bob, carol), ordered by username.
            var firstPage = await GetUsersAsync(authClient, "?page=1&pageSize=2");
            Assert.Equal(4, firstPage.TotalCount);
            Assert.Equal(1, firstPage.Page);
            Assert.Equal(2, firstPage.PageSize);
            Assert.Equal(new[] { "adminuser", "alice" }, firstPage.Users.Select(u => u.Username));

            var secondPage = await GetUsersAsync(authClient, "?page=2&pageSize=2");
            Assert.Equal(4, secondPage.TotalCount);
            Assert.Equal(new[] { "bob", "carol" }, secondPage.Users.Select(u => u.Username));
        }

        [Fact]
        public async Task GetUsers_IncludesPlayerSummaries()
        {
            using var authClient = await SetupAdminClientAsync();
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var seeded = await TestDataSeeder.CreateUserAsync(context, "withplayer", "pw");
                await TestDataSeeder.CreatePlayerAsync(context, seeded.Id, name: "Hero", level: 7);
            }

            var results = await GetUsersAsync(authClient, "?search=withplayer");
            var user = Assert.Single(results.Users, u => u.Username == "withplayer");
            var player = Assert.Single(user.Players);
            Assert.Equal("Hero", player.Name);
            Assert.Equal(7, player.Level);
            Assert.NotEqual(default, player.LastActivity);
        }

        [Fact]
        public async Task GetUsers_SearchMatchesPlayerName()
        {
            using var authClient = await SetupAdminClientAsync();
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var seeded = await TestDataSeeder.CreateUserAsync(context, "zed", "pw");
                await TestDataSeeder.CreatePlayerAsync(context, seeded.Id, name: "Wizard");
            }

            // The username "zed" does not contain the search term, but the player's name does.
            var results = await GetUsersAsync(authClient, "?search=wiz");
            var user = Assert.Single(results.Users, u => u.Username == "zed");
            Assert.Contains(user.Players, p => p.Name == "Wizard");
        }

        [Fact]
        public async Task GetUsers_SearchFiltersByUsernameCaseInsensitively()
        {
            using var authClient = await SetupAdminClientAsync();
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                await TestDataSeeder.CreateUserAsync(context, "Alice", "pw");
                await TestDataSeeder.CreateUserAsync(context, "Alvin", "pw");
                await TestDataSeeder.CreateUserAsync(context, "bob", "pw");
            }

            var results = await GetUsersAsync(authClient, "?search=AL");
            Assert.Equal(2, results.TotalCount);
            Assert.Equal(new[] { "Alice", "Alvin" }, results.Users.Select(u => u.Username));
        }

        [Fact]
        public async Task GetUsers_FilterByRole_ReturnsOnlyUsersInRole()
        {
            using var authClient = await SetupAdminClientAsync();
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var elevated = await TestDataSeeder.CreateUserAsync(context, "elevated", "pw");
                await TestDataSeeder.CreateUserAsync(context, "plain", "pw");
                await TestDataSeeder.AssignRoleToUserAsync(context, elevated.Id, ERole.Admin);
            }

            var results = await GetUsersAsync(authClient, $"?roleId={(int)ERole.Admin}");
            Assert.Contains(results.Users, u => u.Username == "elevated");
            Assert.DoesNotContain(results.Users, u => u.Username == "plain");
            Assert.All(results.Users, u => Assert.Contains(u.Roles, r => r.Name == nameof(ERole.Admin)));
        }

        [Fact]
        public async Task GetUsers_WithNoArchivedParameter_ReturnsAllUsers()
        {
            using var authClient = await SetupAdminClientAsync();
            int ghostId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var ghost = await TestDataSeeder.CreateUserAsync(context, "ghost", "pw");
                ghostId = ghost.Id;
            }

            var beforeArchive = await GetUsersAsync(authClient);
            Assert.Contains(beforeArchive.Users, u => u.Username == "ghost");

            var archiveResponse = await authClient.PostAsJsonAsync(
                "/api/AdminTools/ArchiveUser", new { UserId = ghostId }, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);

            var afterArchive = await GetUsersAsync(authClient);
            Assert.Contains(afterArchive.Users, u => u.Username == "ghost");
            Assert.Equal(beforeArchive.TotalCount, afterArchive.TotalCount);
        }

        [Fact]
        public async Task GetUsers_WithArchivedTrue_ReturnsOnlyArchivedUsers()
        {
            using var authClient = await SetupAdminClientAsync();
            int ghostId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var ghost = await TestDataSeeder.CreateUserAsync(context, "ghost", "pw");
                ghostId = ghost.Id;
            }

            var beforeArchive = await GetUsersAsync(authClient, "?archived=true");
            Assert.DoesNotContain(beforeArchive.Users, u => u.Username == "ghost");

            var archiveResponse = await authClient.PostAsJsonAsync(
                "/api/AdminTools/ArchiveUser", new { UserId = ghostId }, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);

            var afterArchive = await GetUsersAsync(authClient, "?archived=true");
            Assert.Contains(afterArchive.Users, u => u.Username == "ghost");
            Assert.Equal(beforeArchive.TotalCount + 1, afterArchive.TotalCount);
        }

        [Fact]
        public async Task GetUsers_WithArchivedFalse_ReturnsOnlyActiveUsers()
        {
            using var authClient = await SetupAdminClientAsync();
            int ghostId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var ghost = await TestDataSeeder.CreateUserAsync(context, "ghost", "pw");
                ghostId = ghost.Id;
            }

            var beforeArchive = await GetUsersAsync(authClient, "?archived=false");
            Assert.Contains(beforeArchive.Users, u => u.Username == "ghost");

            var archiveResponse = await authClient.PostAsJsonAsync(
                "/api/AdminTools/ArchiveUser", new { UserId = ghostId }, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);

            var afterArchive = await GetUsersAsync(authClient, "?archived=false");
            Assert.DoesNotContain(afterArchive.Users, u => u.Username == "ghost");
            Assert.Equal(beforeArchive.TotalCount - 1, afterArchive.TotalCount);
        }

        [Fact]
        public async Task GetRoles_ReturnsSeededRoles()
        {
            using var authClient = await SetupAdminClientAsync();

            var response = await authClient.GetAsync("/api/AdminTools/GetRoles", CancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiEnumerableResponse<ApiRole>>(CancellationToken);
            Assert.NotNull(result?.Data);
            Assert.Contains(result.Data, r => r.Id == (int)ERole.Admin && r.Name == nameof(ERole.Admin));
        }

        [Fact]
        public async Task SetUserRoles_GrantsAndRevokesRoles()
        {
            using var authClient = await SetupAdminClientAsync();
            int targetId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var target = await TestDataSeeder.CreateUserAsync(context, "promote", "pw");
                targetId = target.Id;
            }

            // Grant the Admin role.
            var grant = await authClient.PostAsJsonAsync(
                "/api/AdminTools/SetUserRoles",
                new { UserId = targetId, RoleIds = new[] { (int)ERole.Admin } },
                CancellationToken);
            Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
            Assert.Equal(new[] { nameof(ERole.Admin) }, await LoadRoleNamesAsync(targetId));

            // Revoke all roles.
            var revoke = await authClient.PostAsJsonAsync(
                "/api/AdminTools/SetUserRoles",
                new { UserId = targetId, RoleIds = Array.Empty<int>() },
                CancellationToken);
            Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
            Assert.Empty(await LoadRoleNamesAsync(targetId));
        }

        [Fact]
        public async Task SetUserRoles_DuplicateValidRoleIds_GrantsRoleOnce()
        {
            using var authClient = await SetupAdminClientAsync();
            int targetId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var target = await TestDataSeeder.CreateUserAsync(context, "dupe", "pw");
                targetId = target.Id;
            }

            // The relocated validation dedups submitted ids, so repeating a valid role is not rejected.
            var response = await authClient.PostAsJsonAsync(
                "/api/AdminTools/SetUserRoles",
                new { UserId = targetId, RoleIds = new[] { (int)ERole.Admin, (int)ERole.Admin } },
                CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(new[] { nameof(ERole.Admin) }, await LoadRoleNamesAsync(targetId));
        }

        [Fact]
        public async Task SetUserRoles_UnknownUser_ReturnsError()
        {
            using var authClient = await SetupAdminClientAsync();

            var response = await authClient.PostAsJsonAsync(
                "/api/AdminTools/SetUserRoles",
                new { UserId = 999999, RoleIds = new[] { (int)ERole.Admin } },
                CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.Equal("User not found.", result?.ErrorMessage);
        }

        [Fact]
        public async Task SetUserRoles_UnknownRole_ReturnsError()
        {
            using var authClient = await SetupAdminClientAsync();
            int targetId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var target = await TestDataSeeder.CreateUserAsync(context, "target", "pw");
                targetId = target.Id;
            }

            var response = await authClient.PostAsJsonAsync(
                "/api/AdminTools/SetUserRoles",
                new { UserId = targetId, RoleIds = new[] { 999 } },
                CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.Equal("One or more roles do not exist.", result?.ErrorMessage);
        }

        [Fact]
        public async Task ArchiveUser_FreesUsernameForReuse()
        {
            await SeedDefaultSkillsAsync();
            using var authClient = await SetupAdminClientAsync();
            int recyclerId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var recycler = await TestDataSeeder.CreateUserAsync(context, "recycler", "pw");
                recyclerId = recycler.Id;
            }

            var archive = await authClient.PostAsJsonAsync(
                "/api/AdminTools/ArchiveUser", new { UserId = recyclerId }, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, archive.StatusCode);

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var archived = await context.Users.AsNoTracking().FirstAsync(u => u.Id == recyclerId, CancellationToken);
                Assert.NotNull(archived.ArchivedAt);
            }

            // The freed username can now be claimed by a brand-new account.
            var createResponse = await Client.PostAsJsonAsync(
                "/api/Login/CreateAccount", new { Username = "recycler", Password = "newpass" }, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
            var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.Null(createResult?.ErrorMessage);
        }

        [Fact]
        public async Task BanUser_KeepsUsernameReserved()
        {
            using var authClient = await SetupAdminClientAsync();
            int outlawId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var outlaw = await TestDataSeeder.CreateUserAsync(context, "outlaw", "pw");
                outlawId = outlaw.Id;
            }

            var ban = await authClient.PostAsJsonAsync(
                "/api/AdminTools/BanUser", new { UserId = outlawId }, CancellationToken);
            Assert.Equal(HttpStatusCode.OK, ban.StatusCode);

            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var banned = await context.Users.AsNoTracking().FirstAsync(u => u.Id == outlawId, CancellationToken);
                Assert.NotNull(banned.BannedAt);
            }

            // A banned user keeps their username reserved, so it cannot be reused.
            var createResponse = await Client.PostAsJsonAsync(
                "/api/Login/CreateAccount", new { Username = "outlaw", Password = "newpass" }, CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
        }

        [Fact]
        public async Task AdminUsers_Unauthenticated_Returns401()
        {
            var response = await Client.GetAsync("/api/AdminTools/GetUsers", CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task AdminUsers_AuthenticatedWithoutAdminRole_Returns403()
        {
            using var authClient = await SetupAdminClientAsync(admin: false);
            var response = await authClient.GetAsync("/api/AdminTools/GetUsers", CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task ArchiveUser_TargetingSelf_IsRejectedAndDoesNotArchive()
        {
            var (authClient, adminId) = await SetupAdminClientWithIdAsync();
            using var client = authClient;

            var response = await client.PostAsJsonAsync(
                "/api/AdminTools/ArchiveUser", new { UserId = adminId }, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.Equal("You cannot archive your own account.", result?.ErrorMessage);

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var admin = await context.Users.AsNoTracking().FirstAsync(u => u.Id == adminId, CancellationToken);
            Assert.Null(admin.ArchivedAt);
        }

        [Fact]
        public async Task BanUser_TargetingSelf_IsRejectedAndDoesNotBan()
        {
            var (authClient, adminId) = await SetupAdminClientWithIdAsync();
            using var client = authClient;

            var response = await client.PostAsJsonAsync(
                "/api/AdminTools/BanUser", new { UserId = adminId }, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.Equal("You cannot ban your own account.", result?.ErrorMessage);

            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var admin = await context.Users.AsNoTracking().FirstAsync(u => u.Id == adminId, CancellationToken);
            Assert.Null(admin.BannedAt);
        }

        [Fact]
        public async Task SetUserRoles_RemovingOwnAdminRole_IsRejectedAndRetainsRole()
        {
            var (authClient, adminId) = await SetupAdminClientWithIdAsync();
            using var client = authClient;

            var response = await client.PostAsJsonAsync(
                "/api/AdminTools/SetUserRoles",
                new { UserId = adminId, RoleIds = Array.Empty<int>() },
                CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse>(CancellationToken);
            Assert.Equal("You cannot remove your own Admin role.", result?.ErrorMessage);

            Assert.Equal(new[] { nameof(ERole.Admin) }, await LoadRoleNamesAsync(adminId));
        }

        [Fact]
        public async Task SetUserRoles_RemovingAdminFromAnotherUser_WhileAdminsRemain_Succeeds()
        {
            var (authClient, _) = await SetupAdminClientWithIdAsync();
            using var client = authClient;
            int otherAdminId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var other = await TestDataSeeder.CreateUserAsync(context, "otheradmin", "pw");
                otherAdminId = other.Id;
                await TestDataSeeder.AssignRoleToUserAsync(context, other.Id, ERole.Admin);
            }

            // The acting admin remains an admin, so demoting another admin is not a lockout.
            var response = await client.PostAsJsonAsync(
                "/api/AdminTools/SetUserRoles",
                new { UserId = otherAdminId, RoleIds = Array.Empty<int>() },
                CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(await LoadRoleNamesAsync(otherAdminId));
        }

        [Fact]
        public async Task SetUserRoles_RemovingAdminFromLastAdmin_IsRejected()
        {
            // Driven through the repository so the actor is a valid-token admin who is no longer a persisted
            // admin (e.g. concurrently demoted), leaving the target as the last admin standing.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var users = scope.ServiceProvider.GetRequiredService<IUsers>();

            var soleAdmin = await TestDataSeeder.CreateUserAsync(context, "soleadmin", "pw");
            await TestDataSeeder.AssignRoleToUserAsync(context, soleAdmin.Id, ERole.Admin);
            var actor = await TestDataSeeder.CreateUserAsync(context, "ghostactor", "pw");

            var status = await users.SetUserRoles(actor.Id, soleAdmin.Id, Array.Empty<int>());

            Assert.Equal(SetUserRolesStatus.LastAdmin, status);
        }

        [Fact]
        public async Task SetUserRoles_RemovingAdminWhenOnlyOtherAdminIsBanned_IsRejected()
        {
            // The only other admin is banned, so it is not a usable admin that could recover the instance.
            // Removing the target's Admin role would strand the system with only banned admins, so the
            // last-admin guard — sharing the usable-admin definition with the archive/ban guard — rejects it.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var users = scope.ServiceProvider.GetRequiredService<IUsers>();

            var target = await TestDataSeeder.CreateUserAsync(context, "cleanadmin", "pw");
            await TestDataSeeder.AssignRoleToUserAsync(context, target.Id, ERole.Admin);

            var bannedAdmin = await TestDataSeeder.CreateUserAsync(context, "bannedadmin", "pw");
            await TestDataSeeder.AssignRoleToUserAsync(context, bannedAdmin.Id, ERole.Admin);
            bannedAdmin.BannedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken);

            var actor = await TestDataSeeder.CreateUserAsync(context, "ghostactor", "pw");

            var status = await users.SetUserRoles(actor.Id, target.Id, Array.Empty<int>());

            Assert.Equal(SetUserRolesStatus.LastAdmin, status);
            // The rejected change must not mutate state — the target keeps Admin.
            Assert.Equal(new[] { nameof(ERole.Admin) }, await LoadRoleNamesAsync(target.Id));
        }

        [Fact]
        public async Task SetUserRoles_ConcurrentDemotionsOfDistinctAdmins_LeaveOneAdminStanding()
        {
            // Two admins are the only admins; a ghost actor concurrently demotes each. The atomic
            // check-then-act must let at most one demotion through so the system is never left admin-less —
            // the exact race the row-locked, in-tier commit exists to prevent.
            int firstAdminId;
            int secondAdminId;
            int actorId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var first = await TestDataSeeder.CreateUserAsync(context, "admin_a", "pw");
                var second = await TestDataSeeder.CreateUserAsync(context, "admin_b", "pw");
                var actor = await TestDataSeeder.CreateUserAsync(context, "ghostactor", "pw");
                firstAdminId = first.Id;
                secondAdminId = second.Id;
                actorId = actor.Id;
                await TestDataSeeder.AssignRoleToUserAsync(context, first.Id, ERole.Admin);
                await TestDataSeeder.AssignRoleToUserAsync(context, second.Id, ERole.Admin);
            }

            // Each demotion runs on its own scope/context (and DB connection) so the two truly race.
            async Task<SetUserRolesStatus> DemoteAsync(int targetId)
            {
                using var scope = CreateScope();
                var users = scope.ServiceProvider.GetRequiredService<IUsers>();
                return await users.SetUserRoles(actorId, targetId, Array.Empty<int>());
            }

            var results = await Task.WhenAll(DemoteAsync(firstAdminId), DemoteAsync(secondAdminId));

            // Exactly one demotion succeeds; the other is rejected by the last-admin guard.
            Assert.Equal(1, results.Count(r => r == SetUserRolesStatus.Success));
            Assert.Equal(1, results.Count(r => r == SetUserRolesStatus.LastAdmin));

            // The database is left with exactly one admin — never zero.
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var remainingAdmins = await context.Users
                    .CountAsync(u => u.ArchivedAt == null && u.Roles.Any(r => r.Id == (int)ERole.Admin), CancellationToken);
                Assert.Equal(1, remainingAdmins);
            }
        }

        [Fact]
        public async Task ArchiveUser_ArchivingAnotherAdmin_WhileAdminsRemain_Succeeds()
        {
            var (authClient, _) = await SetupAdminClientWithIdAsync();
            using var client = authClient;
            int otherAdminId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var other = await TestDataSeeder.CreateUserAsync(context, "otheradmin", "pw");
                otherAdminId = other.Id;
                await TestDataSeeder.AssignRoleToUserAsync(context, other.Id, ERole.Admin);
            }

            // The acting admin remains usable, so archiving another admin is not a lockout.
            var response = await client.PostAsJsonAsync(
                "/api/AdminTools/ArchiveUser", new { UserId = otherAdminId }, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var scope2 = CreateScope();
            var context2 = scope2.ServiceProvider.GetRequiredService<GameContext>();
            var archived = await context2.Users.AsNoTracking().FirstAsync(u => u.Id == otherAdminId, CancellationToken);
            Assert.NotNull(archived.ArchivedAt);
        }

        [Fact]
        public async Task BanUser_BanningAnotherAdmin_WhileAdminsRemain_Succeeds()
        {
            var (authClient, _) = await SetupAdminClientWithIdAsync();
            using var client = authClient;
            int otherAdminId;
            using (var scope = CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<GameContext>();
                var other = await TestDataSeeder.CreateUserAsync(context, "otheradmin", "pw");
                otherAdminId = other.Id;
                await TestDataSeeder.AssignRoleToUserAsync(context, other.Id, ERole.Admin);
            }

            // The acting admin remains usable, so banning another admin is not a lockout.
            var response = await client.PostAsJsonAsync(
                "/api/AdminTools/BanUser", new { UserId = otherAdminId }, CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var scope2 = CreateScope();
            var context2 = scope2.ServiceProvider.GetRequiredService<GameContext>();
            var banned = await context2.Users.AsNoTracking().FirstAsync(u => u.Id == otherAdminId, CancellationToken);
            Assert.NotNull(banned.BannedAt);
        }

        [Fact]
        public async Task ArchiveUser_ArchivingLastAdmin_IsRejected()
        {
            // Driven through the repository so the actor is a valid-token admin who is no longer a usable
            // admin (e.g. concurrently demoted), leaving the target as the last admin standing.
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var users = scope.ServiceProvider.GetRequiredService<IUsers>();

            var soleAdmin = await TestDataSeeder.CreateUserAsync(context, "soleadmin", "pw");
            await TestDataSeeder.AssignRoleToUserAsync(context, soleAdmin.Id, ERole.Admin);
            var actor = await TestDataSeeder.CreateUserAsync(context, "ghostactor", "pw");

            var status = await users.ArchiveUser(actor.Id, soleAdmin.Id);

            Assert.Equal(UserActionStatus.LastAdmin, status);
            var reloaded = await context.Users.AsNoTracking().FirstAsync(u => u.Id == soleAdmin.Id, CancellationToken);
            Assert.Null(reloaded.ArchivedAt);
        }

        [Fact]
        public async Task BanUser_BanningLastAdmin_IsRejected()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var users = scope.ServiceProvider.GetRequiredService<IUsers>();

            var soleAdmin = await TestDataSeeder.CreateUserAsync(context, "soleadmin", "pw");
            await TestDataSeeder.AssignRoleToUserAsync(context, soleAdmin.Id, ERole.Admin);
            var actor = await TestDataSeeder.CreateUserAsync(context, "ghostactor", "pw");

            var status = await users.BanUser(actor.Id, soleAdmin.Id);

            Assert.Equal(UserActionStatus.LastAdmin, status);
            var reloaded = await context.Users.AsNoTracking().FirstAsync(u => u.Id == soleAdmin.Id, CancellationToken);
            Assert.Null(reloaded.BannedAt);
        }

        [Fact]
        public async Task ArchiveUser_ConcurrentMutualArchives_LeaveOneAdminStanding()
        {
            // Two admins are the only admins; a ghost actor concurrently archives each. The atomic
            // check-then-act must let at most one through so the system is never left admin-less.
            var (firstAdminId, secondAdminId, actorId) = await SeedTwoAdminsAndGhostActorAsync();

            async Task<UserActionStatus> ArchiveAsync(int targetId)
            {
                using var scope = CreateScope();
                var users = scope.ServiceProvider.GetRequiredService<IUsers>();
                return await users.ArchiveUser(actorId, targetId);
            }

            var results = await Task.WhenAll(ArchiveAsync(firstAdminId), ArchiveAsync(secondAdminId));

            Assert.Equal(1, results.Count(r => r == UserActionStatus.Success));
            Assert.Equal(1, results.Count(r => r == UserActionStatus.LastAdmin));
            Assert.Equal(1, await CountUsableAdminsAsync());
        }

        [Fact]
        public async Task BanUser_ConcurrentMutualBans_LeaveOneAdminStanding()
        {
            var (firstAdminId, secondAdminId, actorId) = await SeedTwoAdminsAndGhostActorAsync();

            async Task<UserActionStatus> BanAsync(int targetId)
            {
                using var scope = CreateScope();
                var users = scope.ServiceProvider.GetRequiredService<IUsers>();
                return await users.BanUser(actorId, targetId);
            }

            var results = await Task.WhenAll(BanAsync(firstAdminId), BanAsync(secondAdminId));

            Assert.Equal(1, results.Count(r => r == UserActionStatus.Success));
            Assert.Equal(1, results.Count(r => r == UserActionStatus.LastAdmin));
            Assert.Equal(1, await CountUsableAdminsAsync());
        }

        private async Task<(int FirstAdminId, int SecondAdminId, int ActorId)> SeedTwoAdminsAndGhostActorAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var first = await TestDataSeeder.CreateUserAsync(context, "admin_a", "pw");
            var second = await TestDataSeeder.CreateUserAsync(context, "admin_b", "pw");
            var actor = await TestDataSeeder.CreateUserAsync(context, "ghostactor", "pw");
            await TestDataSeeder.AssignRoleToUserAsync(context, first.Id, ERole.Admin);
            await TestDataSeeder.AssignRoleToUserAsync(context, second.Id, ERole.Admin);
            return (first.Id, second.Id, actor.Id);
        }

        private async Task<int> CountUsableAdminsAsync()
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            return await context.Users.CountAsync(
                u => u.ArchivedAt == null && u.BannedAt == null && u.Roles.Any(r => r.Id == (int)ERole.Admin),
                CancellationToken);
        }

        [Fact]
        public async Task ArchiveUser_NonPositiveUserId_Returns400()
        {
            using var authClient = await SetupAdminClientAsync();

            var response = await authClient.PostAsJsonAsync(
                "/api/AdminTools/ArchiveUser", new { UserId = 0 }, CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task SetUserRoles_NullRoleIds_Returns400()
        {
            using var authClient = await SetupAdminClientAsync();

            var response = await authClient.PostAsJsonAsync(
                "/api/AdminTools/SetUserRoles",
                new { UserId = 1, RoleIds = (int[]?)null },
                CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        private async Task<string[]> LoadRoleNamesAsync(int userId)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var user = await context.Users
                .AsNoTracking()
                .Include(u => u.Roles)
                .FirstAsync(u => u.Id == userId, CancellationToken);
            return user.Roles.Select(r => r.Name).OrderBy(n => n).ToArray();
        }
    }
}
