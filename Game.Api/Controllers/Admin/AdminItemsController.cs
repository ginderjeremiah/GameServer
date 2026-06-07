using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Game.Api.Models.Tags;
using Microsoft.AspNetCore.Mvc;

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
            ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Abstractions.Entities.Item
                {
                    Name = item.Name,
                    Description = item.Description,
                    ItemCategoryId = (int)item.ItemCategoryId,
                    RarityId = (int)item.RarityId,
                    IconPath = item.IconPath,
                }),
                edit: item =>
                {
                    var existing = _items.LookupItem(item.Id);
                    if (existing is not null)
                    {
                        existing.Name = item.Name;
                        existing.Description = item.Description;
                        existing.ItemCategoryId = (int)item.ItemCategoryId;
                        existing.RarityId = (int)item.RarityId;
                        existing.IconPath = item.IconPath;
                        _entityStore.Update(existing);
                    }
                },
                delete: item =>
                {
                    var existing = _items.LookupItem(item.Id);
                    if (existing is not null)
                    {
                        _entityStore.Delete(existing);
                    }
                });

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

            ChangeSetProcessor.Apply(changeData.Changes,
                add: attribute => _entityStore.Insert(new Abstractions.Entities.ItemAttribute
                {
                    ItemId = item.Id,
                    AttributeId = (int)attribute.AttributeId,
                    Amount = attribute.Amount,
                }),
                // Operate on a fresh, navigation-free entity (not the cached one, whose loaded
                // Item back-reference would drag the whole graph into the change tracker).
                edit: attribute =>
                {
                    if (item.ItemAttributes.Any(att => (int)attribute.AttributeId == att.AttributeId))
                    {
                        _entityStore.Update(new Abstractions.Entities.ItemAttribute
                        {
                            ItemId = item.Id,
                            AttributeId = (int)attribute.AttributeId,
                            Amount = attribute.Amount,
                        });
                    }
                },
                delete: attribute =>
                {
                    if (item.ItemAttributes.Any(att => (int)attribute.AttributeId == att.AttributeId))
                    {
                        _entityStore.Delete(new Abstractions.Entities.ItemAttribute
                        {
                            ItemId = item.Id,
                            AttributeId = (int)attribute.AttributeId,
                        });
                    }
                });

            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse AddEditItemModSlots([FromBody] List<Change<ItemModSlot>> changes)
        {
            ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Abstractions.Entities.ItemModSlot
                {
                    ItemId = item.ItemId,
                    ItemModSlotTypeId = (int)item.ItemModSlotTypeId,
                }),
                edit: item => _entityStore.Update(new Abstractions.Entities.ItemModSlot
                {
                    Id = item.Id,
                    ItemId = item.ItemId,
                    ItemModSlotTypeId = (int)item.ItemModSlotTypeId,
                }),
                delete: item => _entityStore.Delete(new Abstractions.Entities.ItemModSlot
                {
                    Id = item.Id,
                }));

            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> SetTagsForItem([FromBody] SetTagsData setTagsData)
        {
            var item = _items.LookupItem(setTagsData.Id);
            if (item is not null)
            {
                _entityStore.Track(item);
                var currentTags = await _tags.GetTagEntitiesForItem(setTagsData.Id).ToListAsync();
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
