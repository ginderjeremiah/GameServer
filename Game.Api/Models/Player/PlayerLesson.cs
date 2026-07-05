namespace Game.Api.Models.Player
{
    /// <summary>Wire model for a player's per-lesson state (spike #1392). Sent only for lessons the player has
    /// at least unlocked — an absent entry means locked, matching the domain/entity convention.</summary>
    public class PlayerLesson : IModel
    {
        public int LessonId { get; set; }
        public DateTime UnlockedAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }
}
