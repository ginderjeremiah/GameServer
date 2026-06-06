using Game.Abstractions.DataAccess;
using Game.Api.Models.Progress;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the full challenge reference-data collection. WebSocket equivalent
    /// of the <c>GET /api/Challenges</c> endpoint.
    /// </summary>
    public class GetChallenges : AbstractReferenceDataCommand<Challenge>
    {
        private readonly IChallenges _challenges;

        public override string Name { get; set; } = nameof(GetChallenges);

        public GetChallenges(IChallenges challenges)
        {
            _challenges = challenges;
        }

        protected override IEnumerable<Challenge> GetReferenceData()
        {
            return _challenges.All().To().Model<Challenge>();
        }
    }
}
