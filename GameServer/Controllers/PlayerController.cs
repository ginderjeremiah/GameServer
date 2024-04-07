using DataAccess;
using GameLibrary;
using GameServer.Auth;
using GameServer.Models.Attributes;
using GameServer.Models.Common;
using GameServer.Models.InventoryItems;
using GameServer.Models.Player;
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
            return Success(Session.PlayerData);
        }

        [HttpGet]
        public ApiResponse<InventoryData> Inventory()
        {
            return Success(new InventoryData(Session.InventoryData));
        }

        [HttpGet]
        public ApiListResponse<LogPreference> LogPreferences()
        {
            return Success(Repositories.LogPreferences.GetPreferences(PlayerId).Select(pref => new LogPreference(pref)));
        }

        [HttpPost]
        public ApiResponse SaveLogPreferences([FromBody] List<LogPreference> prefs)
        {
            Repositories.LogPreferences.SavePreferences(PlayerId, prefs.Select(pref => new DataAccess.Models.LogPreferences.LogPreference
            {
                Name = pref.Name,
                Enabled = pref.Enabled,
            }));
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
        public ApiListResponse<BattlerAttribute> UpdatePlayerStats([FromBody] List<AttributeUpdate> changedAttributes)
        {
            Session.UpdatePlayerAttributes(changedAttributes);
            return Success(Session.Player.Attributes.Select(att => new BattlerAttribute(att)));
        }
    }
}
