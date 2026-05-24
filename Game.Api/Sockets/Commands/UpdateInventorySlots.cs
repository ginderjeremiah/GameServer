using Game.Api.Models.Common;
using Game.Api.Models.InventoryItems;
using Game.Api.Services;
using Game.Application.Services;
using Game.Core.Players.Inventories;

namespace Game.Api.Sockets.Commands
{
    public class UpdateInventorySlots : AbstractSocketCommandWithParams<List<InventoryUpdate>>
    {
        private readonly SessionService _sessionService;
        private readonly PlayerService _playerService;

        public override string Name { get; set; } = nameof(UpdateInventorySlots);

        public UpdateInventorySlots(SessionService sessionService, PlayerService playerService)
        {
            _sessionService = sessionService;
            _playerService = playerService;
        }

        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context)
        {
            var player = await _sessionService.LoadPlayer();
            var success = await _playerService.UpdateInventorySlots(
                player, Parameters!.Cast<IInventoryUpdate>());

            return success ? Success() : Error("Invalid inventory update.");
        }
    }
}
