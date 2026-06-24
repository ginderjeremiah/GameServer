using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Api.Models.Progress;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the connected player's tracked statistics (player progress). Unlike the reference-data
    /// commands this is player-scoped: it resolves the player from the socket session and delegates to
    /// <see cref="IPlayerProgressRepository.GetStatistics"/> (cache-first, so it serves warm cached
    /// progress). Named to stay unambiguous against the reference-data <see cref="GetStatisticTypes"/>
    /// command, which returns statistic metadata rather than the player's values.
    /// </summary>
    public class GetPlayerStatistics : AbstractSocketCommandWithResponseData<IEnumerable<PlayerStatistic>>
    {
        private readonly IPlayerProgressRepository _playerProgress;

        public override string Name { get; set; } = nameof(GetPlayerStatistics);

        public GetPlayerStatistics(IPlayerProgressRepository playerProgress)
        {
            _playerProgress = playerProgress;
        }

        public override async Task<ApiSocketResponse<IEnumerable<PlayerStatistic>>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var stats = await _playerProgress.GetStatistics(context.Session.SelectedPlayerId, cancellationToken);
            return Success(stats.To().Model<PlayerStatistic>());
        }
    }
}
