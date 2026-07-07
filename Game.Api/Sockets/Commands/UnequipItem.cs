using Game.Api.Models.Common;
using Game.Api.Models.InventoryItems;
using Game.Application.Services;
using Game.Core;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Clears the item out of an equipment slot. Like the other player-mutating socket commands (e.g.
    /// <see cref="SetItemFavorite"/>), the change is applied to the cached domain player (the source of
    /// truth) and persisted to the database via the write-behind
    /// <see cref="Game.Core.Players.Events.ItemUnequippedEvent"/>. Living on the socket is what makes this
    /// safe: it is processed in the single per-player command loop alongside the idle battle commands, so
    /// it can no longer lose a concurrent read-modify-write race against a background battle save (#463).
    /// </summary>
    public class UnequipItem : AbstractSocketCommand<EquipItemResponse, EquipRequest>
    {
        private readonly PlayerService _playerService;
        private readonly BattleService _battleService;

        public override string Name { get; set; } = nameof(UnequipItem);

        public UnequipItem(PlayerService playerService, BattleService battleService)
        {
            _playerService = playerService;
            _battleService = battleService;
        }

        public override async Task<ApiSocketResponse<EquipItemResponse>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var player = context.Session.Player;
            var success = await _playerService.UnequipItem(
                player, (EEquipmentSlot)Parameters.EquipmentSlotId, cancellationToken);

            // Both outcomes carry the authoritative post-command rating so the client can always reconcile
            // onto it, mirroring UpdatePlayerStats.
            var result = new EquipItemResponse
            {
                PlayerRating = await _battleService.RatePlayer(player, cancellationToken),
            };

            return success ? Success(result) : ErrorWithData("Failed to unequip item.", result);
        }
    }
}
