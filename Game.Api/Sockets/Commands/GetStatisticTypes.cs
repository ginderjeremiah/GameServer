using Game.Api.Models.Progress;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the intrinsic statistic-type reference-data collection. WebSocket
    /// equivalent of the <c>GET /api/Statistics/StatisticTypes</c> endpoint.
    /// </summary>
    public class GetStatisticTypes : AbstractReferenceDataCommand<StatisticType>
    {
        public override string Name { get; set; } = nameof(GetStatisticTypes);

        protected override IEnumerable<StatisticType> GetReferenceData()
        {
            return Core.Progress.StatisticType.GetAll().To().Model<StatisticType>();
        }
    }
}
