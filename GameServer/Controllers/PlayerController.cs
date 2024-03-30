﻿using DataAccess;
using DataAccess.Models.LogPreferences;
using DataAccess.Models.PlayerAttributes;
using GameLibrary;
using GameServer.Auth;
using GameServer.Models.Common;
using GameServer.Models.Request;
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

        [HttpGet]
        public ApiResponse<SessionPlayer> AllData()
        {
            return Success(Session.PlayerData);
        }

        [HttpGet]
        public ApiResponse<SessionInventory> Inventory()
        {
            return Success(Session.InventoryData);
        }

        [HttpPost]
        public ApiResponse<string> UpdateInventorySlots([FromBody] List<InventoryUpdate> inventory)
        {
            if (Session.TryUpdateInventoryItems(inventory))
            {
                return Success();
            }

            return Error<string>("Unable to set inventory items.");
        }

        [HttpGet]
        public ApiResponse<Dictionary<string, bool>> LogPreferences()
        {
            var preferences = Repositories.LogPreferences.GetPreferences(PlayerId);
            return Success(preferences.ToDictionary(pref => pref.Name, pref => pref.Enabled));
        }

        [HttpPost]
        public ApiResponse<string> SaveLogPreferences([FromBody] Dictionary<string, bool> prefs)
        {
            Repositories.LogPreferences.SavePreferences(PlayerId, prefs.Select(kvp => new LogPreference()
            {
                Name = kvp.Key,
                Enabled = kvp.Value
            }));
            return Success();
        }

        [HttpPost]
        public ApiResponse<List<PlayerAttribute>> UpdatePlayerStats([FromBody] List<AttributeUpdate> changedAttributes)
        {
            Session.UpdatePlayerAttributes(changedAttributes);
            return Success(Session.PlayerData.Attributes);
        }
    }
}
