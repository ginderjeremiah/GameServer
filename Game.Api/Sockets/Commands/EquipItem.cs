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
    public class EquipItem : AbstractSocketCommandWithParams<EquipRequest>
    {
        private readonly PlayerService _playerService;

        public override string Name { get; set; } = nameof(EquipItem);

        public EquipItem(PlayerService playerService)
        {
            _playerService = playerService;
        }

        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var player = context.Session.Player;
            var success = await _playerService.EquipItem(
                player, Parameters.ItemId, (EEquipmentSlot)Parameters.EquipmentSlotId, cancellationToken);

            return success ? Success() : Error("Failed to equip item.");
        }
    }
}
