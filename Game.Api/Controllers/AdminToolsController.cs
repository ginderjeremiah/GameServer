using Game.Api.Models.Common;
using Game.Api.Models.Items;
using Game.Api.Models.Tags;
using Game.Core.DataAccess;
using Microsoft.AspNetCore.Mvc;
using static Game.Api.EChangeType;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class AdminToolsController : ControllerBase
    {
        private readonly IRepositoryManager _repositoryManager;

        public AdminToolsController(IRepositoryManager repositoryManager)
        {
            _repositoryManager = repositoryManager;
        }

        [HttpPost]
        public async Task<ApiResponse> AddEditItemAttributes([FromBody] AddEditItemAttributesData changeData)
        {
            var item = _repositoryManager.Items.GetItem(changeData.ItemId);
            if (item is null)
            {
                return ApiResponse.Error("Item does not exist.");
            }

            foreach (var change in changeData.Changes)
            {
                if (change.ChangeType == Add)
                {
                    item.ItemAttributes.Add(new Core.Entities.ItemAttribute()
                    {
                        ItemId = item.Id,
                        AttributeId = (int)change.Item.AttributeId,
                        Amount = change.Item.Amount,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var att = item.ItemAttributes.FirstOrDefault(att => (int)change.Item.AttributeId == att.AttributeId);
                    if (att is not null)
                    {
                        att.Amount = change.Item.Amount;
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var att = item.ItemAttributes.FirstOrDefault(att => (int)change.Item.AttributeId == att.AttributeId);
                    if (att is not null)
                    {
                        item.ItemAttributes.Remove(att);
                    }
                }
            }

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> AddEditItemModAttributes([FromBody] AddEditItemModAttributesData changeData)
        {
            var itemMod = _repositoryManager.ItemMods.GetItemMod(changeData.ItemModId);
            if (itemMod is null)
            {
                return ApiResponse.Error("Item Mod does not exist.");
            }

            foreach (var change in changeData.Changes)
            {
                if (change.ChangeType == Add)
                {
                    itemMod.ItemModAttributes.Add(new Core.Entities.ItemModAttribute()
                    {
                        ItemModId = itemMod.Id,
                        AttributeId = (int)change.Item.AttributeId,
                        Amount = change.Item.Amount,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var att = itemMod.ItemModAttributes.FirstOrDefault(att => (int)change.Item.AttributeId == att.AttributeId);
                    if (att is not null)
                    {
                        att.Amount = change.Item.Amount;
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var att = itemMod.ItemModAttributes.FirstOrDefault(att => (int)change.Item.AttributeId == att.AttributeId);
                    if (att is not null)
                    {
                        itemMod.ItemModAttributes.Remove(att);
                    }
                }
            }

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> AddEditItemMods([FromBody] List<Change<ItemMod>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == Add)
                {
                    _repositoryManager.Insert(new ItemMod
                    {
                        Name = change.Item.Name,
                        Removable = change.Item.Removable,
                        Description = change.Item.Description,
                        SlotTypeId = change.Item.SlotTypeId,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var itemMod = _repositoryManager.ItemMods.GetItemMod(change.Item.Id);
                    if (itemMod is not null)
                    {
                        itemMod.Name = change.Item.Name;
                        itemMod.Removable = change.Item.Removable;
                        itemMod.Description = change.Item.Description;
                        itemMod.SlotTypeId = change.Item.SlotTypeId;
                        _repositoryManager.Update(itemMod);
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var itemMod = _repositoryManager.ItemMods.GetItemMod(change.Item.Id);
                    if (itemMod is not null)
                    {
                        _repositoryManager.Delete(itemMod);
                    }
                }
            }

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> AddEditItems([FromBody] List<Change<Item>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == Add)
                {
                    _repositoryManager.Insert(new Core.Entities.Item
                    {
                        Name = change.Item.Name,
                        Description = change.Item.Description,
                        ItemCategoryId = (int)change.Item.ItemCategoryId,
                        IconPath = change.Item.IconPath,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var item = _repositoryManager.Items.GetItem(change.Item.Id);
                    if (item is not null)
                    {
                        item.Name = change.Item.Name;
                        item.Description = change.Item.Description;
                        item.ItemCategoryId = (int)change.Item.ItemCategoryId;
                        item.IconPath = change.Item.IconPath;
                        _repositoryManager.Update(item);
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var item = _repositoryManager.Items.GetItem(change.Item.Id);
                    if (item is not null)
                    {
                        _repositoryManager.Delete(item);
                    }
                }
            }

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> AddEditItemModSlots([FromBody] List<Change<ItemModSlot>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == Add)
                {
                    _repositoryManager.Insert(new Core.Entities.ItemModSlot
                    {
                        ItemId = change.Item.ItemId,
                        ItemModSlotTypeId = (int)change.Item.ItemModSlotTypeId,
                        GuaranteedItemModId = change.Item.GuaranteedItemModId,
                        Probability = change.Item.Probability,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    _repositoryManager.Update(new Core.Entities.ItemModSlot
                    {
                        Id = change.Item.Id,
                        ItemId = change.Item.ItemId,
                        ItemModSlotTypeId = (int)change.Item.ItemModSlotTypeId,
                        GuaranteedItemModId = change.Item.GuaranteedItemModId,
                        Probability = change.Item.Probability,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _repositoryManager.Delete(new Core.Entities.ItemModSlot
                    {
                        Id = change.Item.Id,
                    });
                }
            }

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> AddEditTags([FromBody] List<Change<Tag>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == Add)
                {
                    _repositoryManager.Insert(new Core.Entities.Tag
                    {
                        Name = change.Item.Name,
                        TagCategoryId = change.Item.TagCategoryId,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    _repositoryManager.Update(new Core.Entities.Tag
                    {
                        Id = change.Item.Id,
                        Name = change.Item.Name,
                        TagCategoryId = change.Item.TagCategoryId,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _repositoryManager.Delete(new Core.Entities.Tag
                    {
                        Id = change.Item.Id,
                    });
                }
            }

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> SetTagsForItem([FromBody] SetTagsData setTagsData)
        {
            var item = _repositoryManager.Items.GetItem(setTagsData.Id);
            if (item is not null)
            {
                item.Tags.Clear();
                var tags = _repositoryManager.Tags.GetTags(setTagsData.TagIds);
                await foreach (var tag in tags)
                {
                    item.Tags.Add(tag);
                }

                await _repositoryManager.SaveChangesAsync();
                return ApiResponse.Success();
            }

            return ApiResponse.Error("Item not found.");
        }

        [HttpPost]
        public async Task<ApiResponse> SetTagsForItemMod([FromBody] SetTagsData setTagsData)
        {
            var itemMod = _repositoryManager.ItemMods.GetItemMod(setTagsData.Id);
            if (itemMod is not null)
            {
                itemMod.Tags.Clear();
                var tags = _repositoryManager.Tags.GetTags(setTagsData.TagIds);
                await foreach (var tag in tags)
                {
                    itemMod.Tags.Add(tag);
                }

                await _repositoryManager.SaveChangesAsync();
                return ApiResponse.Success();
            }

            return ApiResponse.Error("Item mod not found.");
        }
    }
}
