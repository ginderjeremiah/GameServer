using UserEntity = Game.Abstractions.Entities.User;

namespace Game.Api.Models.Users
{
    /// <summary>
    /// Admin-facing view of a user account, including its granted roles and archive/ban status.
    /// </summary>
    public class AdminUser : IModelFromSource<AdminUser, UserEntity>
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public DateTime LastLogin { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public DateTime? BannedAt { get; set; }
        public required IEnumerable<Role> Roles { get; set; }

        public static AdminUser FromSource(UserEntity user)
        {
            return new AdminUser
            {
                Id = user.Id,
                Username = user.Username,
                LastLogin = user.LastLogin,
                ArchivedAt = user.ArchivedAt,
                BannedAt = user.BannedAt,
                Roles = user.Roles.Select(Role.FromSource).ToList(),
            };
        }
    }
}
