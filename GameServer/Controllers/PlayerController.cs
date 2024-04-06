using DataAccess;
using DataAccess.Models.LogPreferences;
using DataAccess.Models.PlayerAttributes;
using GameLibrary;
using GameServer.Auth;
using GameServer.Models.Common;
using GameServer.Models.Request;
using GameServer.Models.Response;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [SessionAuthorize]
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class PlayerController : BaseController
    {
        public PlayerController(IRepositoryManager repositoryManager, IApiLogger logger)
            : base(repositoryManager, logger) { }

        [HttpGet("/api/[controller]")]
        public ApiResponse<PlayerData> Player()
        {
            return Success(new PlayerData(Session.PlayerData));
        }

        [HttpGet]
        public ApiResponse<InventoryData> Inventory()
        {
            return Success(new InventoryData(Session.InventoryData));
        }

        [HttpGet]
        public ApiResponse<List<LogPreference>> LogPreferences()
        {
            return Success(Repositories.LogPreferences.GetPreferences(PlayerId));
        }

        [HttpPost]
        public ApiResponse SaveLogPreferences([FromBody] List<LogPreference> prefs)
        {
            Repositories.LogPreferences.SavePreferences(PlayerId, prefs);
            return Success();
        }

        [HttpPost]
        public ApiResponse UpdateInventorySlots([FromBody] List<InventoryUpdate> inventory)
        {
            if (Session.TryUpdateInventoryItems(inventory))
            {
                return Success();
            }

            return Error("Unable to set inventory items.");
        }

        [HttpPost]
        public ApiResponse<List<PlayerAttribute>> UpdatePlayerStats([FromBody] List<AttributeUpdate> changedAttributes)
        {
            Session.UpdatePlayerAttributes(changedAttributes);
            return Success(Session.PlayerData.Attributes);
        }
    }
}
