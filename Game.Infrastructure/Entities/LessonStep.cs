namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// One step of a <see cref="Lesson"/>'s coach-mark tour: the callout text and an optional anchor a Svelte
    /// action registers by key (<c>use:tutorialAnchor</c>, #1592). Keyed by (lesson, ordinal); a step with no
    /// <see cref="AnchorKey"/> degrades to a centered, unanchored callout on the frontend.
    /// </summary>
    public class LessonStep
    {
        public int LessonId { get; set; }
        public int Ordinal { get; set; }
        public required string Text { get; set; }
        public string? AnchorKey { get; set; }

        public virtual Lesson Lesson { get => field ?? throw new NotLoadedException(nameof(Lesson)); set; }
    }
}
