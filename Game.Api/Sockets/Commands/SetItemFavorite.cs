using Game.Api.Models.Common;
using Game.Api.Models.InventoryItems;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Toggles whether an unlocked item is favorited. Applies the change to the cached
    /// domain player (the source of truth) and persists it to the database via the
    /// write-behind <see cref="Game.Core.Players.Events.ItemFavoriteChangedEvent"/>.
    /// </summary>
    public class SetItemFavorite : AbstractSocketCommandWithParams<SetItemFavoriteRequest>
    {
        private readonly PlayerService _playerService;

        public override string Name { get; set; } = nameof(SetItemFavorite);

        public SetItemFavorite(PlayerService playerService)
        {
            _playerService = playerService;
        }

        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var player = await context.Session.LoadPlayer();
            var success = await _playerService.SetFavorite(player, Parameters.ItemId, Parameters.Favorite);

            return success ? Success() : Error("Failed to set item favorite.");
        }
    }
}
