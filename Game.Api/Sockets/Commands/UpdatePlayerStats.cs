using Game.Abstractions.Contracts;
using Game.Api.Models.Attributes;
using Game.Api.Models.Common;
using Game.Application.Services;
using Game.Core.Players;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Applies a set of per-attribute stat-point deltas to the player's allocation and returns the
    /// resulting allocations. Like the other player-mutating socket commands (e.g.
    /// <see cref="SetSelectedSkills"/>), the change is applied to the cached domain player (the source of
    /// truth) and persisted to the database via the write-behind
    /// <see cref="Game.Core.Players.Events.AttributeAllocationsChangedEvent"/>. Living on the socket is
    /// what makes this safe: it is processed in the single per-player command loop alongside the idle
    /// battle commands, so it can no longer lose a concurrent read-modify-write race against a background
    /// battle save (the cause of issue #432).
    /// </summary>
    public class UpdatePlayerStats : AbstractSocketCommand<List<BattlerAttribute>, List<AttributeUpdate>>
    {
        private readonly PlayerService _playerService;

        public override string Name { get; set; } = nameof(UpdatePlayerStats);

        public UpdatePlayerStats(PlayerService playerService)
        {
            _playerService = playerService;
        }

        public override async Task<ApiSocketResponse<List<BattlerAttribute>>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var player = context.Session.Player;
            var success = await _playerService.TryUpdateAttributes(player, Parameters.Cast<IAttributeUpdate>(), cancellationToken);

            var allocations = player.StatPoints.StatAllocations
                .Select(allocation => BattlerAttribute.From(allocation.Attribute, allocation.Amount))
                .ToList();

            return success
                ? Success(allocations)
                : ErrorWithData("Unable to update player stats.", allocations);
        }
    }
}
