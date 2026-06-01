using PlayerChallengeEntity = Game.Abstractions.Entities.PlayerChallenge;

namespace Game.Api.Models.Challenges
{
    public class PlayerChallenge : IModelFromSource<PlayerChallenge, PlayerChallengeEntity>
    {
        public int ChallengeId { get; set; }
        public decimal Progress { get; set; }
        public bool Completed { get; set; }
        public DateTime? CompletedAt { get; set; }

        public static PlayerChallenge FromSource(PlayerChallengeEntity entity)
        {
            return new PlayerChallenge
            {
                ChallengeId = entity.ChallengeId,
                Progress = entity.Progress,
                Completed = entity.Completed,
                CompletedAt = entity.CompletedAt,
            };
        }
    }
}
