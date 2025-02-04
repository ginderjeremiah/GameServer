using Game.Abstractions.DataAccess;
using Game.Api.Models.Attributes;
using Game.Api.Models.Common;
using Game.Api.Models.InventoryItems;
using Game.Api.Models.Player;
using Game.Api.Services;
using Game.Core.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class PlayerController : ControllerBase
    {
        private readonly IRepositoryManager _repositoryManager;
        private readonly SessionService _sessionService;

        private Session Session => _sessionService.GetSession();

        public PlayerController(IRepositoryManager repositoryManager, SessionService sessionService)
        {
            _repositoryManager = repositoryManager;
            _sessionService = sessionService;
        }

        [HttpGet("/api/[controller]")]
        public ApiResponse<PlayerData> Player()
        {
            return ApiResponse.Success(Session.GetPlayerData());
        }

        [HttpPost]
        public ApiResponse SaveLogPreferences([FromBody] List<LogPreference> prefs)
        {
            //TODO save LogPreferences to session player and propogate to db
            throw new NotImplementedException();
        }

        [HttpPost]
        public ApiResponse UpdateInventorySlots([FromBody] List<InventoryUpdate> inventory)
        {
            return Session.TryUpdateInventoryItems(inventory.Cast<IInventoryUpdate>())
                ? ApiResponse.Success()
                : ApiResponse.Error("Unable to set inventory items.");
        }

        [HttpPost]
        public ApiEnumerableResponse<BattlerAttribute> UpdatePlayerStats([FromBody] List<AttributeUpdate> changedAttributes)
        {
            Session.UpdatePlayerAttributes(changedAttributes.Cast<IAttributeUpdate>());
            return ApiResponse.Success(Session.BattlerAttributes.To().Model<BattlerAttribute>());
        }
    }
}
