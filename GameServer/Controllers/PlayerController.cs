using GameCore;
using GameCore.Sessions;
using GameServer.Auth;
using GameServer.Models.Attributes;
using GameServer.Models.Common;
using GameServer.Models.InventoryItems;
using GameServer.Models.Player;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class PlayerController : BaseController
    {
        public PlayerController(IRepositoryManager repositoryManager, IApiLogger logger, SessionService sessionService)
            : base(repositoryManager, logger, sessionService) { }

        [SessionAuthorize]
        [HttpGet("/api/[controller]")]
        public ApiResponse<PlayerData> Player()
        {
            return Success(Session.GetPlayerData());
        }

        [SessionAuthorize]
        [HttpGet]
        public ApiListResponse<LogPreference> LogPreferences()
        {
            return Success(Session.Player.LogPreferences.Select(lp => new LogPreference(lp)));
        }

        [SessionAuthorize]
        [HttpPost]
        public ApiResponse SaveLogPreferences([FromBody] List<LogPreference> prefs)
        {
            //TODO get LogPreferences from session player
            throw new NotImplementedException();
        }

        [SessionAuthorize]
        [HttpPost]
        public ApiResponse UpdateInventorySlots([FromBody] List<InventoryUpdate> inventory)
        {
            return Session.TryUpdateInventoryItems(inventory.Cast<IInventoryUpdate>().ToList())
                ? Success()
                : Error("Unable to set inventory items.");
        }

        [SessionAuthorize]
        [HttpPost]
        public ApiListResponse<BattlerAttribute> UpdatePlayerStats([FromBody] List<AttributeUpdate> changedAttributes)
        {
            Session.UpdatePlayerAttributes(changedAttributes.Cast<IAttributeUpdate>().ToList());
            return Success(Session.BattlerAttributes.Select(att => new BattlerAttribute(att)).ToList());
        }
    }
}
