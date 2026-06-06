namespace Game.Abstractions.Entities
{
    /// <summary>
    /// Tracks a user's connections from a distinct origin. Rather than a full ledger, the combination of
    /// <see cref="UserId"/>, <see cref="IpAddress"/>, and <see cref="DeviceId"/> is unique, and
    /// <see cref="LastConnection"/> is continually updated for that combination. Updated on standard HTTP
    /// requests, not on every WebSocket command.
    /// </summary>
    public class UserLogin
    {
        public const int MaxIpAddressLength = 45;

        public int Id { get; set; }

        public int UserId { get; set; }

        /// <summary>The remote IP address the request originated from.</summary>
        public required string IpAddress { get; set; }

        /// <summary>Foreign key to the <see cref="Entities.Device"/> the connection came from.</summary>
        public int DeviceId { get; set; }

        /// <summary>The timestamp of the most recent connection from this user/IP/device combination.</summary>
        public DateTime LastConnection { get; set; }

        public virtual User User { get => field ?? throw new NotLoadedException(nameof(User)); set; }

        public virtual Device Device { get => field ?? throw new NotLoadedException(nameof(Device)); set; }
    }
}
