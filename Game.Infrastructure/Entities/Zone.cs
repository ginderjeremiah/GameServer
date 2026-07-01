namespace Game.Infrastructure.Entities
{
    public class Zone : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public int Order { get; set; }
        public int LevelMin { get; set; }
        public int LevelMax { get; set; }

        /// <summary>The zone's single dedicated boss, fought via the "Challenge Boss" action. Null when no
        /// boss has been authored. Distinct from the random <see cref="ZoneEnemies"/> spawn table.</summary>
        public int? BossEnemyId { get; set; }

        /// <summary>The fixed level the dedicated boss is fought at, independent of <see cref="LevelMin"/>/
        /// <see cref="LevelMax"/>. Only meaningful when <see cref="BossEnemyId"/> is set.</summary>
        public int BossLevel { get; set; }

        /// <summary>The challenge that gates entry to this zone, or null when the zone is always open (e.g.
        /// the starting zone). The zone unlocks once the player completes this challenge. Navigation-less
        /// optional FK, like <see cref="BossEnemyId"/>.</summary>
        public int? UnlockChallengeId { get; set; }

        /// <summary>Whether this is the special <em>Home</em> zone — a no-combat sanctuary the player can
        /// idle in without battling. A Home zone never spawns enemies (authoring guards reject a boss or a
        /// spawn-table membership on it) and is never set as a player's persisted <c>CurrentZoneId</c> (the
        /// battle-start zone-change anti-cheat refuses a move into one), so offline rewards keep crediting the
        /// player's last real combat zone. Authoring/orchestration metadata the battle simulation never reads,
        /// so it stays off the lean gameplay <see cref="Game.Core.Zones.Zone"/> (resolved via
        /// <see cref="Game.Abstractions.DataAccess.IZones.IsHomeZone"/>, like retirement).</summary>
        public bool IsHome { get; set; }

        /// <summary>Authoring-only design rationale (why this piece exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

        /// <summary>When set, the record is <em>retired</em> (see <see cref="Item.RetiredAt"/>).</summary>
        public DateTime? RetiredAt { get; set; }

        public virtual List<ZoneEnemy> ZoneEnemies { get => field ?? throw new NotLoadedException(nameof(ZoneEnemies)); set; }
    }
}
