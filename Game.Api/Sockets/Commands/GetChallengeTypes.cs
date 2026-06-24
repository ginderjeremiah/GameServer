using Game.Api.Models.Progress;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Serves the intrinsic challenge-type reference-data set over the socket.
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
