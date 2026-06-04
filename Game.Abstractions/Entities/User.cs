namespace Game.Abstractions.Entities
{
    public class User
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public Guid Salt { get; set; }
        public required string PassHash { get; set; }
        public DateTime LastLogin { get; set; }

        /// <summary>
        /// When set, the user has been archived (soft-deleted). An archived user is excluded from
        /// login, username-availability checks, and the admin user roster, which frees their
        /// username for reuse by another account.
        /// </summary>
        public DateTime? ArchivedAt { get; set; }

        /// <summary>
        /// When set, the user has been banned. Unlike archiving, a banned user still occupies their
        /// username (it is not freed for reuse) and remains visible in the admin roster.
        /// </summary>
        public DateTime? BannedAt { get; set; }

        public virtual List<Player> Players { get => field ?? throw new NotLoadedException(nameof(Players)); set; }
        public virtual List<Role> Roles { get => field ?? throw new NotLoadedException(nameof(Roles)); set; }
    }
}
