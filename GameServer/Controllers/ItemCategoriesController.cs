﻿using GameCore;
using GameServer.Models.Common;
using GameServer.Models.Items;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class ItemCategoriesController : BaseController
    {
        public ItemCategoriesController(IRepositoryManager repositoryManager, IApiLogger logger)
            : base(repositoryManager, logger) { }

        [HttpGet("/api/[controller]")]
        public ApiListResponse<ItemCategory> ItemCategories()
        {
            return Success(Repositories.ItemCategories.GetItemCategories().Select(cat => new ItemCategory(cat)));
        }
    }
}
