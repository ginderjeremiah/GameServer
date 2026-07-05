namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// Per-player tutorial-lesson state (spike #1392). Absence of a row means locked; <see cref="ReadAt"/> null
    /// vs. set distinguishes unread from read.
    /// </summary>
    public class PlayerLesson
    {
        public int PlayerId { get; set; }
        public int LessonId { get; set; }
        public DateTime UnlockedAt { get; set; }
        public DateTime? ReadAt { get; set; }

        public virtual Player Player { get => field ?? throw new NotLoadedException(nameof(Player)); set; }
        public virtual Lesson Lesson { get => field ?? throw new NotLoadedException(nameof(Lesson)); set; }
    }
}
