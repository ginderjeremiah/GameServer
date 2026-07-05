namespace Game.Core.Players
{
    /// <summary>
    /// Per-player tutorial-lesson state (spike #1392). Absence from <see cref="Player.Lessons"/> means locked;
    /// <see cref="UnlockedAt"/> set means unread; <see cref="ReadAt"/> also set means read.
    /// </summary>
    public class PlayerLesson
    {
        public required int LessonId { get; set; }
        public required DateTime UnlockedAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
