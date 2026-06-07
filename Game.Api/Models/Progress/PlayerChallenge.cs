using CorePlayerChallenge = Game.Core.Progress.PlayerChallenge;

namespace Game.Api.Models.Progress
{
    public class PlayerChallenge : IModelFromSource<PlayerChallenge, CorePlayerChallenge>
    {
        public int ChallengeId { get; set; }
        public decimal Progress { get; set; }
        public bool Completed { get; set; }
        public DateTime? CompletedAt { get; set; }

        public static PlayerChallenge FromSource(CorePlayerChallenge source)
        {
            return new PlayerChallenge
            {
                ChallengeId = source.Challenge.Id,
                Progress = source.Progress,
                Completed = source.Completed,
                CompletedAt = source.CompletedAt,
            };
        }
    }
}
