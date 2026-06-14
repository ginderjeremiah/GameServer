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
    public class UnequipItem : AbstractSocketCommandWithParams<EquipRequest>
    {
        private readonly PlayerService _playerService;

        public override string Name { get; set; } = nameof(UnequipItem);

        public UnequipItem(PlayerService playerService)
        {
            _playerService = playerService;
        }

        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var player = context.Session.Player;
            var success = await _playerService.UnequipItem(
                player, (EEquipmentSlot)Parameters.EquipmentSlotId);

            return success ? Success() : Error("Failed to unequip item.");
        }
    }
}
