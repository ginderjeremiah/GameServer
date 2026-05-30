using Game.Api.Models.Common;
using Game.Api.Models.InventoryItems;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Toggles whether an unlocked item is favorited. Persists the change on the
    /// cached domain player (the source of truth for player data).
    /// </summary>
    public class SetItemFavorite : AbstractSocketCommandWithParams<SetItemFavoriteRequest>
    {
        private readonly PlayerService _playerService;

        public override string Name { get; set; } = nameof(SetItemFavorite);

        public SetItemFavorite(PlayerService playerService)
        {
            _playerService = playerService;
        }

        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context)
        {
            var player = await context.Session.LoadPlayer();
            var success = await _playerService.SetFavorite(player, Parameters.ItemId, Parameters.Favorite);

            return success ? Success() : Error("Failed to set item favorite.");
        }
    }
}
