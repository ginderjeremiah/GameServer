using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Game.Api.Models.Items;
using Game.Api.Models.Tags;
using Microsoft.AspNetCore.Mvc;
using static Game.Api.EChangeType;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting item mods and their related collections
    /// (attributes and tags). The route prefix is shared across every admin controller so the
    /// existing <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminCacheInvalidationFilter))]
    public class AdminItemModsController(
        IItemMods itemMods,
        ITags tags,
        IEntityStore entityStore) : ControllerBase
    {
        private readonly IItemMods _itemMods = itemMods;
        private readonly ITags _tags = tags;
        private readonly IEntityStore _entityStore = entityStore;

        [HttpPost]
        public ApiResponse AddEditItemMods([FromBody] List<Change<ItemMod>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Abstractions.Entities.ItemMod
                    {
                        Name = change.Item.Name,
                        Description = change.Item.Description,
                        ItemModTypeId = (int)change.Item.ItemModTypeId,
                        RarityId = (int)change.Item.RarityId,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var itemMod = _itemMods.LookupItemMod(change.Item.Id);
                    if (itemMod is not null)
                    {
                        itemMod.Name = change.Item.Name;
                        itemMod.Description = change.Item.Description;
                        itemMod.ItemModTypeId = (int)change.Item.ItemModTypeId;
                        itemMod.RarityId = (int)change.Item.RarityId;
                        _entityStore.Update(itemMod);
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var itemMod = _itemMods.LookupItemMod(change.Item.Id);
                    if (itemMod is not null)
                    {
                        _entityStore.Delete(itemMod);
                    }
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse AddEditItemModAttributes([FromBody] AddEditAttributesData changeData)
        {
            var itemMod = _itemMods.LookupItemMod(changeData.Id);
            if (itemMod is null)
            {
                return ApiResponse.Error("Item Mod does not exist.");
            }

            foreach (var change in changeData.Changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Abstractions.Entities.ItemModAttribute
                    {
                        ItemModId = itemMod.Id,
                        AttributeId = (int)change.Item.AttributeId,
                        Amount = change.Item.Amount,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    // Operate on a fresh, navigation-free entity (not the cached one, whose loaded
                    // ItemMod back-reference would drag the whole graph into the change tracker).
                    if (itemMod.ItemModAttributes.Any(att => (int)change.Item.AttributeId == att.AttributeId))
                    {
                        _entityStore.Update(new Abstractions.Entities.ItemModAttribute
                        {
                            ItemModId = itemMod.Id,
                            AttributeId = (int)change.Item.AttributeId,
                            Amount = change.Item.Amount,
                        });
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    if (itemMod.ItemModAttributes.Any(att => (int)change.Item.AttributeId == att.AttributeId))
                    {
                        _entityStore.Delete(new Abstractions.Entities.ItemModAttribute
                        {
                            ItemModId = itemMod.Id,
                            AttributeId = (int)change.Item.AttributeId,
                        });
                    }
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> SetTagsForItemMod([FromBody] SetTagsData setTagsData)
        {
            var itemMod = _itemMods.LookupItemMod(setTagsData.Id);
            if (itemMod is not null)
            {
                _entityStore.Track(itemMod);
                var currentTags = await _tags.GetTagsForItemMod(setTagsData.Id).ToListAsync();
                foreach (var currentTag in currentTags)
                {
                    if (!setTagsData.TagIds.Contains(currentTag.Id))
                    {
                        currentTag.ItemMods.Clear();
                    }
                }

                var tags = _tags.GetTags(setTagsData.TagIds);
                await foreach (var tag in tags)
                {
                    if (!currentTags.Any(t => t.Id == tag.Id))
                    {
                        tag.ItemMods = [];
                        tag.ItemMods.Add(itemMod);
                    }
                }

                return ApiResponse.Success();
            }

            return ApiResponse.Error("Item mod not found.");
        }
    }
}
