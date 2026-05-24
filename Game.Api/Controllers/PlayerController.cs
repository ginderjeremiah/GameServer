using Game.Api.Models.Attributes;
using Game.Api.Models.Common;
using Game.Api.Models.InventoryItems;
using Game.Api.Models.Player;
using Game.Api.Services;
using Game.Application.Services;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class PlayerController : ControllerBase
    {
        private readonly SessionService _sessionService;
        private readonly PlayerService _playerService;

        public PlayerController(SessionService sessionService, PlayerService playerService)
        {
            _sessionService = sessionService;
            _playerService = playerService;
        }

        [HttpGet("/api/[controller]")]
        public async Task<ApiResponse<PlayerData>> Player()
        {
            var player = await _sessionService.LoadPlayer();
            return ApiResponse.Success(PlayerData.FromPlayer(player));
        }

        [HttpPost]
        public ApiResponse SaveLogPreferences([FromBody] List<LogPreference> prefs)
        {
            throw new NotImplementedException();
        }

        [HttpPost]
        public async Task<ApiResponse> UpdateInventorySlots([FromBody] List<InventoryUpdate> inventory)
        {
            var player = await _sessionService.LoadPlayer();
            var success = await _playerService.UpdateInventorySlots(
                player, inventory.Cast<IInventoryUpdate>());

            return success
                ? ApiResponse.Success()
                : ApiResponse.Error("Invalid inventory update.");
        }

        [HttpPost]
        public async Task<ApiEnumerableResponse<BattlerAttribute>> UpdatePlayerStats([FromBody] List<AttributeUpdate> changedAttributes)
        {
            var player = await _sessionService.LoadPlayer();
            var success = await _playerService.TryUpdateAttributes(player, changedAttributes.Cast<IAttributeUpdate>());
            if (!success)
            {
                return ApiResponse.Error("Unable to update player stats.");
            }

            var attributes = player.GetAttributes();
            var result = attributes.AllModifiers()
                .GroupBy(m => m.Attribute)
                .Select(g => new BattlerAttribute
                {
                    AttributeId = g.Key,
                    Amount = (decimal)attributes[g.Key],
                });

            return ApiResponse.Success(result);
        }
    }
}
