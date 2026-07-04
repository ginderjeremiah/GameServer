using Game.Core;

namespace Game.Infrastructure.Entities
{
    /// <summary>A tutorial lesson (#1591, spike #1392): reference data with a two-kind trigger
    /// (screen-visit or mechanic-event) and an ordered list of steps. Per-player unread/read state is a
    /// separate concern (#1588); client trigger evaluation is #1587.</summary>
    public class Lesson : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }

        /// <summary>Stable authoring key (e.g. "first-crit"), unique among live lessons.</summary>
        public required string Key { get; set; }

        /// <summary>Display name shown in the Help screen (#1589).</summary>
        public required string Name { get; set; }

        public ELessonTriggerType TriggerType { get; set; }

        /// <summary>Set iff <see cref="TriggerType"/> is <see cref="ELessonTriggerType.ScreenVisit"/>: the
        /// frontend `ScreenDef.key` (UI/src/routes/game/screens/screen-defs.ts) whose first activation fires
        /// this lesson. Not FK-checkable — screens are a frontend-only registry with no backend table.</summary>
        public string? TriggerScreenKey { get; set; }

        /// <summary>Set iff <see cref="TriggerType"/> is <see cref="ELessonTriggerType.MechanicEvent"/>.</summary>
        public int? TriggerMechanicEventId { get; set; }

        /// <summary>The screen key this lesson's tour plays on (always required, regardless of trigger kind).</summary>
        public required string HostScreenKey { get; set; }

        /// <summary>Display ordering for the Help screen (#1589).</summary>
        public int DisplayOrder { get; set; }

        public DateTime? RetiredAt { get; set; }

        public virtual MechanicEvent? TriggerMechanicEvent { get; set; }

        public virtual List<LessonStep> Steps { get => field ?? throw new NotLoadedException(nameof(Steps)); set; }
    }
}
