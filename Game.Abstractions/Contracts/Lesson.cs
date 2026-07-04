using Game.Core;

namespace Game.Abstractions.Contracts
{
    public class Lesson : IModel
    {
        public int Id { get; set; }
        public required string Key { get; set; }
        public required string Name { get; set; }
        public ELessonTriggerType TriggerType { get; set; }
        public string? TriggerScreenKey { get; set; }
        public EMechanicEvent? TriggerMechanicEvent { get; set; }
        public required string HostScreenKey { get; set; }
        public int DisplayOrder { get; set; }
        public DateTime? RetiredAt { get; set; }

        /// <summary>Ordered by position; the identity save ignores this collection (persisted through the
        /// dedicated <c>SetLessonSteps</c> setter, mirroring the skill-recipe editor).</summary>
        public required IEnumerable<LessonStep> Steps { get; set; }
    }

    public class LessonStep
    {
        public required string Text { get; set; }
        public string? AnchorKey { get; set; }
    }
}
