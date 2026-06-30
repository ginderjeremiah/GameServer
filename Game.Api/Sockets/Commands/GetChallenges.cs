using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;
using Game.Application.Content;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Serves the full challenge reference-data set over the socket.
    /// </summary>
    public class GetChallenges : AbstractReferenceDataCommand<Challenge>
    {
        private readonly IChallenges _challenges;

        public override string Name { get; set; } = nameof(GetChallenges);

        public GetChallenges(IChallenges challenges)
        {
            _challenges = challenges;
        }

        // The domain challenge carries the rich ChallengeType; the shared mapper flattens it to the read
        // contract (the gameplay domain IChallenges.All intentionally serves the domain model — see backend.md).
        protected override IEnumerable<Challenge> GetReferenceData()
        {
            return _challenges.All().Select(ChallengeContractMapper.ToContract);
        }

        protected override object VersionKey => _challenges.VersionKey;
    }
}
