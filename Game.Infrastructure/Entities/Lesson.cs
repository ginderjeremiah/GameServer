namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// A tutorial lesson (spike #1392): a screen- or mechanic-anchored coach-mark tour. Static, authored
    /// reference data with a zero-based identity, riding the same content export/seed pipeline and Workbench
    /// CRUD as every other content entity. Its steps are the ordered <see cref="LessonStep"/> rows carrying
    /// this lesson's id.
    /// </summary>
    public class Lesson : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }

        /// <summary>Stable authoring slug (e.g. <c>"idle-loop-basics"</c>), distinct from the player-facing
        /// <see cref="Name"/> — the progression-graph lint matches content-design's taught-by-blurb candidates
        /// against this, not against display text that authors are free to reword.</summary>
        public required string Key { get; set; }

        /// <summary>Player-facing title, shown in the Help screen's lesson list.</summary>
        public required string Name { get; set; }

        /// <summary>What fires this lesson's tour — a screen visit or a mechanic event.</summary>
        public int TriggerType { get; set; }

        /// <summary>The screen the tour plays on. Doubles as the trigger target when
        /// <see cref="TriggerType"/> is <see cref="Core.ELessonTriggerType.ScreenVisit"/>. A plain string, not
        /// an FK: screens are a frontend-only registry (<c>screen-defs.ts</c>) with no backend representation.</summary>
        public required string ScreenKey { get; set; }

        /// <summary>The mechanic event that fires this lesson, set iff <see cref="TriggerType"/> is
        /// <see cref="Core.ELessonTriggerType.MechanicEvent"/>; null for a screen-anchored lesson.</summary>
        public int? TriggerMechanicEvent { get; set; }

        /// <summary>Display order in the Help screen's lesson list — independent of <see cref="Id"/>, so
        /// reordering doesn't require renumbering ids.</summary>
        public int Ordinal { get; set; }

        /// <summary>Authoring-only design rationale (why this lesson exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

        /// <summary>When set, the record is <em>retired</em> (see <see cref="Item.RetiredAt"/>).</summary>
        public DateTime? RetiredAt { get; set; }

        public virtual List<LessonStep> Steps { get => field ?? throw new NotLoadedException(nameof(Steps)); set; }
    }
}
