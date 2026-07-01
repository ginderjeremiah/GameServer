namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// A path: an ordered sequence of proficiencies sharing an identity arc (e.g. Fire Magic → Inferno
    /// Magic) and the unit a battle quantity trains (see the proficiency-progression spike,
    /// <c>docs/spikes/1318-proficiency-progression-avenues.md</c>). Static, authored reference data with a
    /// zero-based identity. Its tiers are the <see cref="Proficiency"/> rows carrying this path's id, ordered
    /// by <see cref="Proficiency.PathOrdinal"/>; a standalone proficiency is just a path of length one.
    /// </summary>
    public class Path : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }

        /// <summary>
        /// The activity this path trains on — an <see cref="Core.EActivityKey"/> stored as its int value (a
        /// plain enum-as-int column, like <see cref="Skill.Acquisition"/>): a damage-type key, category, or
        /// combat event. At battle completion the effect-based accrual sums the battle's activity for this key
        /// and routes it to the path's frontier tier (spike #1318). Existing rows backfill to <c>0</c>
        /// (<see cref="Core.EActivityKey.Physical"/>).
        /// </summary>
        public int ActivityKey { get; set; }

        /// <summary>Authoring-only design rationale (why this piece exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

        /// <summary>When set, the record is <em>retired</em> (see <see cref="Item.RetiredAt"/>).</summary>
        public DateTime? RetiredAt { get; set; }

        public virtual List<Proficiency> Proficiencies { get => field ?? throw new NotLoadedException(nameof(Proficiencies)); set; }
    }
}
