namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a zone in the reference-data catalogue.</summary>
    public class Zone : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public int Order { get; set; }
        public int LevelMin { get; set; }
        public int LevelMax { get; set; }

        /// <summary>The zone's single dedicated boss enemy, fought via the "Challenge Boss" action.
        /// Null when no boss has been authored.</summary>
        public int? BossEnemyId { get; set; }

        /// <summary>The fixed level the dedicated boss is fought at. Only meaningful when
        /// <see cref="BossEnemyId"/> is set.</summary>
        public int BossLevel { get; set; }

        /// <summary>The challenge that gates entry to this zone, or null when the zone is always open. The
        /// zone unlocks once the player completes this challenge.</summary>
        public int? UnlockChallengeId { get; set; }

        /// <summary>Whether this is the special <em>Home</em> zone — a no-combat sanctuary the player can idle
        /// in without battling. A Home zone never spawns enemies and never becomes the player's persisted
        /// current zone (offline rewards keep crediting their last real combat zone). Defaults to false.</summary>
        public bool IsHome { get; set; }

        /// <summary>When set, the record is retired (out of circulation but kept resolvable by id).
        /// Null while active.</summary>
        public DateTime? RetiredAt { get; set; }
    }
}
