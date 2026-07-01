namespace Game.Infrastructure.Entities
{
    public class Enemy : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public bool IsBoss { get; set; }

        /// <summary>Authoring-only design rationale (why this piece exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

        /// <summary>When set, the record is <em>retired</em> (see <see cref="Item.RetiredAt"/>). A retired
        /// enemy is excluded from random spawn rolls but still resolves by id (e.g. as an authored boss).</summary>
        public DateTime? RetiredAt { get; set; }

        public virtual List<AttributeDistribution> AttributeDistributions { get => field ?? throw new NotLoadedException(nameof(AttributeDistributions)); set; }
        public virtual List<EnemySkill> EnemySkills { get => field ?? throw new NotLoadedException(nameof(EnemySkills)); set; }
        public virtual List<ZoneEnemy> ZoneEnemies { get => field ?? throw new NotLoadedException(nameof(ZoneEnemies)); set; }
    }
}
