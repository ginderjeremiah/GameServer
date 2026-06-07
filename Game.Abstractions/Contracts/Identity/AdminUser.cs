namespace Game.Abstractions.Contracts.Identity
{
    /// <summary>
    /// Admin-facing read contract for a user account, including its granted roles and archive/ban
    /// status. The published read language of the Identity / User Admin context — the admin Workbench
    /// consumes it; the EF entity behind it never leaves <c>Game.DataAccess</c>.
    /// </summary>
    public class AdminUser : IModel
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public DateTime LastLogin { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public DateTime? BannedAt { get; set; }
        public required IEnumerable<Role> Roles { get; set; }
        public required IEnumerable<PlayerSummary> Players { get; set; }
    }
}
