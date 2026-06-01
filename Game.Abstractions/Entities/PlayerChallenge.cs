namespace Game.Abstractions.Entities
{
    public class PlayerChallenge
    {
        public int PlayerId { get; set; }
        public int ChallengeId { get; set; }
        public decimal Progress { get; set; }
        public bool Completed { get; set; }
        public DateTime? CompletedAt { get; set; }

        public virtual Player Player { get => field ?? throw new NotLoadedException(nameof(Player)); set; }
        public virtual Challenge Challenge { get => field ?? throw new NotLoadedException(nameof(Challenge)); set; }
    }
}
