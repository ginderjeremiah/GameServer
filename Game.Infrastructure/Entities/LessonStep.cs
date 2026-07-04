namespace Game.Infrastructure.Entities
{
    public class LessonStep
    {
        public int Id { get; set; }
        public int LessonId { get; set; }

        /// <summary>0-based position within the lesson; the (LessonId, Order) pair is unique.</summary>
        public int Order { get; set; }

        public required string Text { get; set; }

        /// <summary>Optional key the frontend maps to a registered UI anchor target (#1592). Frontend-side
        /// validation that it resolves to a real anchor is owned by #1592, not this lint.</summary>
        public string? AnchorKey { get; set; }

        public virtual Lesson Lesson { get => field ?? throw new NotLoadedException(nameof(Lesson)); set; }
    }
}
