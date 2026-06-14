using Game.Api.Models.Common;
using Game.Api.Models.InventoryItems;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Applies an unlocked modifier to one of an item's mod slots. Like the other player-mutating socket
    /// commands (e.g. <see cref="SetItemFavorite"/>), the change is applied to the cached domain player
    /// (the source of truth) and persisted to the database via the write-behind
    /// <see cref="Game.Core.Players.Events.ModAppliedEvent"/>. Living on the socket is what makes this
    /// safe: it is processed in the single per-player command loop alongside the idle battle commands, so
    /// it can no longer lose a concurrent read-modify-write race against a background battle save (#463).
    /// </summary>
    public class ApplyMod : AbstractSocketCommandWithParams<ApplyModRequest>
    {
        private readonly PlayerService _playerService;

        public override string Name { get; set; } = nameof(ApplyMod);

        public ApplyMod(PlayerService playerService)
        {
            _playerService = playerService;
        }

        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context)
        {
            var player = await context.Session.LoadPlayer();
            var success = await _playerService.ApplyMod(
                player, Parameters.ItemId, Parameters.ItemModId, Parameters.ItemModSlotId);

            return success ? Success() : Error("Failed to apply modifier.");
        }
    }
}
