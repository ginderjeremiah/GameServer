using Game.Abstractions.Contracts;
using Game.Api.Models.Attributes;
using Game.Api.Models.Common;
using Game.Application.Services;
using Game.Core.Players;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Applies a set of per-attribute stat-point deltas to the player's allocation and returns the
    /// resulting allocations plus the authoritative <c>StatPointsUsed</c> (so the client reconciles the
    /// spend absolutely rather than re-deriving it). Like the other player-mutating socket commands (e.g.
    /// <see cref="SetSelectedSkills"/>), the change is applied to the cached domain player (the source of
    /// truth) and persisted to the database via the write-behind
    /// <see cref="Game.Core.Players.Events.AttributeAllocationsChangedEvent"/>. Living on the socket is
    /// what makes this safe: it is processed in the single per-player command loop alongside the idle
    /// battle commands, so it can no longer lose a concurrent read-modify-write race against a background
    /// battle save (the cause of issue #432).
    /// </summary>
    public class UpdatePlayerStats : AbstractSocketCommand<UpdatePlayerStatsResponse, List<AttributeUpdate>>
    {
        private readonly PlayerService _playerService;
        private readonly BattleService _battleService;

        public override string Name { get; set; } = nameof(UpdatePlayerStats);

        public UpdatePlayerStats(PlayerService playerService, BattleService battleService)
        {
            _playerService = playerService;
            _battleService = battleService;
        }

        public override async Task<ApiSocketResponse<UpdatePlayerStatsResponse>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var player = context.Session.Player;
            var success = await _playerService.TryUpdateAttributes(player, Parameters.Cast<IAttributeUpdate>(), cancellationToken);

            // Both outcomes carry the authoritative post-command state (unchanged on the error path), so
            // the client can always reconcile onto it.
            var result = new UpdatePlayerStatsResponse
            {
                Attributes = player.StatPoints.StatAllocations
                    .Select(allocation => BattlerAttribute.From(allocation.Attribute, allocation.Amount))
                    .ToList(),
                StatPointsUsed = player.StatPoints.StatPointsUsed,
                PlayerRating = await _battleService.RatePlayer(player, cancellationToken),
            };

            return success
                ? Success(result)
                : ErrorWithData("Unable to update player stats.", result);
        }
    }
}
