using Game.Api.Models.Progress;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the intrinsic challenge-type reference-data collection. WebSocket
    /// equivalent of the <c>GET /api/Challenges/ChallengeTypes</c> endpoint.
    /// </summary>
    public class GetChallengeTypes : AbstractReferenceDataCommand<ChallengeType>
    {
        public override string Name { get; set; } = nameof(GetChallengeTypes);

        protected override IEnumerable<ChallengeType> GetReferenceData()
        {
            return Core.Progress.ChallengeType.GetAll().To().Model<ChallengeType>();
        }

        // Intrinsic (enum-derived) set: fixed for the process lifetime, so the version is memoized once.
        protected override object VersionKey => IntrinsicVersionKey<GetChallengeTypes>.Instance;
    }
}
