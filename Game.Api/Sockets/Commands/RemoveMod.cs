using Game.Api.Models.Common;
using Game.Api.Models.InventoryItems;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Removes the modifier occupying one of an item's mod slots. Like the other player-mutating socket
    /// commands (e.g. <see cref="SetItemFavorite"/>), the change is applied to the cached domain player
    /// (the source of truth) and persisted to the database via the write-behind
    /// <see cref="Game.Core.Players.Events.ModRemovedEvent"/>. Living on the socket is what makes this
    /// safe: it is processed in the single per-player command loop alongside the idle battle commands, so
    /// it can no longer lose a concurrent read-modify-write race against a background battle save (#463).
    /// </summary>
    public class RemoveMod : AbstractSocketCommandWithParams<RemoveModRequest>
    {
        private readonly PlayerService _playerService;

        public override string Name { get; set; } = nameof(RemoveMod);

        public RemoveMod(PlayerService playerService)
        {
            _playerService = playerService;
        }

        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var player = context.Session.Player;
            var success = await _playerService.RemoveMod(
                player, Parameters.ItemId, Parameters.ItemModSlotId);

            return success ? Success() : Error("Failed to remove modifier.");
        }
    }
}
