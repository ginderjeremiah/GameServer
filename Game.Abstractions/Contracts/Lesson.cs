using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read/authoring contract for a tutorial lesson (spike #1392): a screen- or mechanic-anchored
    /// coach-mark tour. The steps are <see cref="LessonStep"/> records ordered within the lesson.</summary>
    public class Lesson : IModel, IHasDesignerNotes
    {
        public int Id { get; set; }

        /// <summary>Stable authoring slug the progression-graph lint matches against — distinct from the
        /// player-facing <see cref="Name"/>.</summary>
        public required string Key { get; set; }

        /// <summary>Player-facing title, shown in the Help screen's lesson list.</summary>
        public required string Name { get; set; }

        /// <summary>What fires this lesson's tour.</summary>
        public ELessonTriggerType TriggerType { get; set; }

        /// <summary>The screen the tour plays on; also the trigger target when <see cref="TriggerType"/> is
        /// <see cref="ELessonTriggerType.ScreenVisit"/>.</summary>
        public required string ScreenKey { get; set; }

        /// <summary>The mechanic event that fires this lesson, set iff <see cref="TriggerType"/> is
        /// <see cref="ELessonTriggerType.MechanicEvent"/>; null for a screen-anchored lesson.</summary>
        public EMechanicEvent? TriggerMechanicEvent { get; set; }

        /// <summary>Display order in the Help screen's lesson list.</summary>
        public int Ordinal { get; set; }

        /// <summary>Authoring-only design rationale (why this lesson exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

        /// <summary>When set, the record is retired (out of circulation but kept resolvable by id).</summary>
        public DateTime? RetiredAt { get; set; }

        /// <summary>The tour's steps, ordered.</summary>
        public required IEnumerable<LessonStep> Steps { get; set; }
    }
}
