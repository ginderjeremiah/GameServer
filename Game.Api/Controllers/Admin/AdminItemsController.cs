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
    /// Admin Workbench endpoints for persisting items and their related collections
    /// (attributes, item-mod slots, and tags). The route prefix is shared across every
    /// admin controller so the existing <c>/api/AdminTools/*</c> contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
    [ServiceFilter(typeof(AdminCacheInvalidationFilter))]
    public class AdminItemsController(
        IItems items,
        ITags tags,
        IEntityStore entityStore) : ControllerBase
    {
        private readonly IItems _items = items;
        private readonly ITags _tags = tags;
        private readonly IEntityStore _entityStore = entityStore;

        [HttpPost]
        public ApiResponse AddEditItems([FromBody] List<Change<Item>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Abstractions.Entities.Item
                    {
                        Name = change.Item.Name,
                        Description = change.Item.Description,
                        ItemCategoryId = (int)change.Item.ItemCategoryId,
                        RarityId = (int)change.Item.RarityId,
                        IconPath = change.Item.IconPath,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var item = _items.LookupItem(change.Item.Id);
                    if (item is not null)
                    {
                        item.Name = change.Item.Name;
                        item.Description = change.Item.Description;
                        item.ItemCategoryId = (int)change.Item.ItemCategoryId;
                        item.RarityId = (int)change.Item.RarityId;
                        item.IconPath = change.Item.IconPath;
                        _entityStore.Update(item);
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var item = _items.LookupItem(change.Item.Id);
                    if (item is not null)
                    {
                        _entityStore.Delete(item);
                    }
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse AddEditItemAttributes([FromBody] AddEditAttributesData changeData)
        {
            var item = _items.LookupItem(changeData.Id);
            if (item is null)
            {
                return ApiResponse.Error("Item does not exist.");
            }

            foreach (var change in changeData.Changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Abstractions.Entities.ItemAttribute
                    {
                        ItemId = item.Id,
                        AttributeId = (int)change.Item.AttributeId,
                        Amount = change.Item.Amount,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    // Operate on a fresh, navigation-free entity (not the cached one, whose loaded
                    // Item back-reference would drag the whole graph into the change tracker).
                    if (item.ItemAttributes.Any(att => (int)change.Item.AttributeId == att.AttributeId))
                    {
                        _entityStore.Update(new Abstractions.Entities.ItemAttribute
                        {
                            ItemId = item.Id,
                            AttributeId = (int)change.Item.AttributeId,
                            Amount = change.Item.Amount,
                        });
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    if (item.ItemAttributes.Any(att => (int)change.Item.AttributeId == att.AttributeId))
                    {
                        _entityStore.Delete(new Abstractions.Entities.ItemAttribute
                        {
                            ItemId = item.Id,
                            AttributeId = (int)change.Item.AttributeId,
                        });
                    }
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse AddEditItemModSlots([FromBody] List<Change<ItemModSlot>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Abstractions.Entities.ItemModSlot
                    {
                        ItemId = change.Item.ItemId,
                        ItemModSlotTypeId = (int)change.Item.ItemModSlotTypeId,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    _entityStore.Update(new Abstractions.Entities.ItemModSlot
                    {
                        Id = change.Item.Id,
                        ItemId = change.Item.ItemId,
                        ItemModSlotTypeId = (int)change.Item.ItemModSlotTypeId,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _entityStore.Delete(new Abstractions.Entities.ItemModSlot
                    {
                        Id = change.Item.Id,
                    });
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> SetTagsForItem([FromBody] SetTagsData setTagsData)
        {
            var item = _items.LookupItem(setTagsData.Id);
            if (item is not null)
            {
                _entityStore.Track(item);
                var currentTags = await _tags.GetTagsForItem(setTagsData.Id).ToListAsync();
                foreach (var currentTag in currentTags)
                {
                    if (!setTagsData.TagIds.Contains(currentTag.Id))
                    {
                        currentTag.Items.Clear();
                    }
                }

                var tags = _tags.GetTags(setTagsData.TagIds);
                await foreach (var tag in tags)
                {
                    if (!currentTags.Any(t => t.Id == tag.Id))
                    {
                        tag.Items = [];
                        tag.Items.Add(item);
                    }
                }

                return ApiResponse.Success();
            }

            return ApiResponse.Error("Item not found.");
        }
    }
}
