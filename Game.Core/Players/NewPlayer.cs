namespace Game.Core.Players
{
    /// <summary>
    /// A blueprint describing the initial state of a freshly created player — the game's answer to
    /// "what does a new player look like?" (starter skills, base attribute spread, and the default
    /// log preferences). Produced by <see cref="NewPlayerFactory"/> and translated into a persisted
    /// entity graph by the data-access layer; it deliberately carries no identity or resolved
    /// reference-data objects, only the raw defaults.
    /// </summary>
    public class NewPlayer
    {
        /// <summary>The class chosen at character creation — the archetype that parameterized this
        /// blueprint (starter kit + attribute spread). Persisted as the permanent <see cref="Player.ClassId"/>
        /// seam.</summary>
        public required int ClassId { get; init; }
        public required string Name { get; init; }
        public required int Level { get; init; }
        public required int Exp { get; init; }
        public required int CurrentZoneId { get; init; }
        public required int StatPointsGained { get; init; }
        public required int StatPointsUsed { get; init; }
        public required IReadOnlyList<NewPlayerSkill> Skills { get; init; }

        /// <summary>The items the player starts with unlocked and equipped (from the class kit).</summary>
        public required IReadOnlyList<NewPlayerEquipment> Equipment { get; init; }

        public required IReadOnlyList<StatAllocation> Attributes { get; init; }
        public required IReadOnlyList<LogPreference> LogPreferences { get; init; }
    }
}
