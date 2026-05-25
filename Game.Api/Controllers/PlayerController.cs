using Game.Api.Models.Attributes;
using Game.Api.Models.Common;
using Game.Api.Models.InventoryItems;
using Game.Api.Models.Player;
using Game.Api.Services;
using Game.Application.Services;
using Game.Core;
using Game.Core.Players;
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
        public ApiResponse SaveLogPreferences([FromBody] List<Models.Player.LogPreference> prefs)
        {
            throw new NotImplementedException();
        }

        [HttpPost]
        public async Task<ApiResponse> EquipItem([FromBody] EquipRequest request)
        {
            var player = await _sessionService.LoadPlayer();
            var success = await _playerService.EquipItem(
                player, request.ItemId, (EEquipmentSlot)request.EquipmentSlotId);

            return success
                ? ApiResponse.Success()
                : ApiResponse.Error("Failed to equip item.");
        }

        [HttpPost]
        public async Task<ApiResponse> UnequipItem([FromBody] EquipRequest request)
        {
            var player = await _sessionService.LoadPlayer();
            var success = await _playerService.UnequipItem(
                player, (EEquipmentSlot)request.EquipmentSlotId);

            return success
                ? ApiResponse.Success()
                : ApiResponse.Error("Failed to unequip item.");
        }

        [HttpPost]
        public async Task<ApiResponse> ApplyMod([FromBody] ApplyModRequest request)
        {
            var player = await _sessionService.LoadPlayer();
            var success = await _playerService.ApplyMod(
                player, request.ItemId, request.ItemModId, request.ItemModSlotId);

            return success
                ? ApiResponse.Success()
                : ApiResponse.Error("Failed to apply modifier.");
        }

        [HttpPost]
        public async Task<ApiResponse> RemoveMod([FromBody] RemoveModRequest request)
        {
            var player = await _sessionService.LoadPlayer();
            var success = await _playerService.RemoveMod(
                player, request.ItemId, request.ItemModSlotId);

            return success
                ? ApiResponse.Success()
                : ApiResponse.Error("Failed to remove modifier.");
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

            var result = player.StatPoints.StatAllocations
                .Select(a => new BattlerAttribute
                {
                    AttributeId = a.Attribute,
                    Amount = (decimal)a.Amount,
                });

            return ApiResponse.Success(result);
        }
    }
}
