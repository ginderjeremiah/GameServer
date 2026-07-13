using Game.Api.Models.Common;
using Game.Api.Models.InventoryItems;
using Game.Application.Services;
using Game.Core;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Equips an unlocked item into an equipment slot. Like the other player-mutating socket commands
    /// (e.g. <see cref="SetItemFavorite"/>), the change is applied to the cached domain player (the source
    /// of truth) and persisted to the database via the write-behind
    /// <see cref="Game.Core.Players.Events.ItemEquippedEvent"/>. Living on the socket is what makes this
    /// safe: it is processed in the single per-player command loop alongside the idle battle commands, so
    /// it can no longer lose a concurrent read-modify-write race against a background battle save (#463).
    /// </summary>
    public class EquipItem : AbstractSocketCommand<EquipItemResponse, EquipRequest>
    {
        private readonly PlayerService _playerService;
        private readonly BattleService _battleService;

        public override string Name { get; set; } = nameof(EquipItem);

        public EquipItem(PlayerService playerService, BattleService battleService)
        {
            _playerService = playerService;
            _battleService = battleService;
        }

        public override async Task<ApiSocketResponse<EquipItemResponse>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var player = context.Session.Player;
            var (success, proficiencyLevels) = await _playerService.EquipItem(
                player, Parameters.ItemId, (EEquipmentSlot)Parameters.EquipmentSlotId, cancellationToken);

            // Both outcomes carry the authoritative post-command rating so the client can always reconcile
            // onto it, mirroring UpdatePlayerStats. Reuses the proficiency levels the gear gate above already
            // loaded rather than re-reading the same Redis hash (#1729).
            var result = new EquipItemResponse
            {
                PlayerRating = _battleService.RatePlayer(player, proficiencyLevels),
            };

            return success ? Success(result) : ErrorWithData("Failed to equip item.", result);
        }
    }
}
