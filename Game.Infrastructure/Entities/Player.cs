using Game.Core.Players;

namespace Game.Infrastructure.Entities
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

        /// <inheritdoc cref="Core.Players.Player.ClassId"/>
        public int ClassId { get; set; }

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

        /// <inheritdoc cref="Core.Players.Player.AutoChallengeBoss"/>
        public bool AutoChallengeBoss { get; set; }

        public virtual User User { get => field ?? throw new NotLoadedException(nameof(User)); set; }
        public virtual List<PlayerAttribute> PlayerAttributes { get => field ?? throw new NotLoadedException(nameof(PlayerAttributes)); set; }
        public virtual List<UnlockedItem> UnlockedItems { get => field ?? throw new NotLoadedException(nameof(UnlockedItems)); set; }
        public virtual List<UnlockedMod> UnlockedMods { get => field ?? throw new NotLoadedException(nameof(UnlockedMods)); set; }
        public virtual List<AppliedMod> AppliedMods { get => field ?? throw new NotLoadedException(nameof(AppliedMods)); set; }
        public virtual List<PlayerChallenge> PlayerChallenges { get => field ?? throw new NotLoadedException(nameof(PlayerChallenges)); set; }
        public virtual List<PlayerProficiency> PlayerProficiencies { get => field ?? throw new NotLoadedException(nameof(PlayerProficiencies)); set; }
        public virtual List<PlayerStatistic> PlayerStatistics { get => field ?? throw new NotLoadedException(nameof(PlayerStatistics)); set; }
        public virtual List<LogPreference> LogPreferences { get => field ?? throw new NotLoadedException(nameof(LogPreferences)); set; }
        public virtual List<PlayerSkill> PlayerSkills { get => field ?? throw new NotLoadedException(nameof(PlayerSkills)); set; }
        public virtual List<PlayerLesson> PlayerLessons { get => field ?? throw new NotLoadedException(nameof(PlayerLessons)); set; }
    }
}
