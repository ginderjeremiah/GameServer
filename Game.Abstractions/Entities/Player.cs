using Game.Core.Players;

namespace Game.Abstractions.Entities
{
    /// <summary>
    /// A database entity for a <see cref="Core.Players.Player"/>.
    /// </summary>
    public class Player
    {
        /// <summary>
        /// The unique identifier of the player.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The unique identifier of the <see cref="Entities.User"/> that owns this player.
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// The unique identifier of the <see cref="Entities.User"/> that the player is currently in.
        /// </summary>
        public int CurrentZoneId { get; set; }

        /// <inheritdoc cref="Core.Players.Player.Name"/>
        public required string Name { get; set; }

        /// <inheritdoc cref="Core.Players.Player.Level"/>
        public required int Level { get; set; }

        /// <inheritdoc cref="Core.Players.Player.Exp"/>
        public required int Exp { get; set; }

        /// <inheritdoc cref="PlayerStatPoints.StatPointsGained"/>
        public required int StatPointsGained { get; set; }

        /// <inheritdoc cref="PlayerStatPoints.StatPointsUsed"/>
        public required int StatPointsUsed { get; set; }

        /// <summary>
        /// The date and time of the last activity of the player.
        /// </summary>
        public DateTime LastActivity { get; set; }

        public virtual User User { get; set; }
        public virtual List<PlayerAttribute> PlayerAttributes { get; set; } = [];
        public virtual List<InventoryItem> InventoryItems { get; set; } = [];
        public virtual List<LogPreference> LogPreferences { get; set; } = [];
        public virtual List<PlayerSkill> PlayerSkills { get; set; } = [];
    }
}
