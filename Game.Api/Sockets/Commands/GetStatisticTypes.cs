using Game.Api.Models.Progress;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Serves the intrinsic statistic-type reference-data set over the socket.
    /// </summary>
    public class GetStatisticTypes : AbstractReferenceDataCommand<StatisticType>
    {
        public override string Name { get; set; } = nameof(GetStatisticTypes);

        protected override IEnumerable<StatisticType> GetReferenceData()
        {
            return Core.Progress.StatisticType.GetAll().To().Model<StatisticType>();
        }

        // Intrinsic (enum-derived) set: fixed for the process lifetime, so the version is memoized once.
        protected override object VersionKey => IntrinsicVersionKey<GetStatisticTypes>.Instance;
    }
}
