using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Game.Api.Models.Tags;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting item mods and their related collections
    /// (attributes and tags). The route prefix is shared across every admin controller so the
    /// existing <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
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
            ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Abstractions.Entities.ItemMod
                {
                    Name = item.Name,
                    Description = item.Description,
                    ItemModTypeId = (int)item.ItemModTypeId,
                    RarityId = (int)item.RarityId,
                }),
                edit: item =>
                {
                    var existing = _itemMods.LookupItemMod(item.Id);
                    if (existing is not null)
                    {
                        existing.Name = item.Name;
                        existing.Description = item.Description;
                        existing.ItemModTypeId = (int)item.ItemModTypeId;
                        existing.RarityId = (int)item.RarityId;
                        _entityStore.Update(existing);
                    }
                },
                delete: item =>
                {
                    var existing = _itemMods.LookupItemMod(item.Id);
                    if (existing is not null)
                    {
                        _entityStore.Delete(existing);
                    }
                });

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

            ChangeSetProcessor.Apply(changeData.Changes,
                add: attribute => _entityStore.Insert(new Abstractions.Entities.ItemModAttribute
                {
                    ItemModId = itemMod.Id,
                    AttributeId = (int)attribute.AttributeId,
                    Amount = attribute.Amount,
                }),
                // Operate on a fresh, navigation-free entity (not the cached one, whose loaded
                // ItemMod back-reference would drag the whole graph into the change tracker).
                edit: attribute =>
                {
                    if (itemMod.ItemModAttributes.Any(att => (int)attribute.AttributeId == att.AttributeId))
                    {
                        _entityStore.Update(new Abstractions.Entities.ItemModAttribute
                        {
                            ItemModId = itemMod.Id,
                            AttributeId = (int)attribute.AttributeId,
                            Amount = attribute.Amount,
                        });
                    }
                },
                delete: attribute =>
                {
                    if (itemMod.ItemModAttributes.Any(att => (int)attribute.AttributeId == att.AttributeId))
                    {
                        _entityStore.Delete(new Abstractions.Entities.ItemModAttribute
                        {
                            ItemModId = itemMod.Id,
                            AttributeId = (int)attribute.AttributeId,
                        });
                    }
                });

            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> SetTagsForItemMod([FromBody] SetTagsData setTagsData)
        {
            var itemMod = _itemMods.LookupItemMod(setTagsData.Id);
            if (itemMod is not null)
            {
                _entityStore.Track(itemMod);
                var currentTags = await _tags.GetTagEntitiesForItemMod(setTagsData.Id).ToListAsync();
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
