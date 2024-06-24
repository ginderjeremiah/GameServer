using GameCore;
using GameServer.Auth;
using GameServer.Models.Common;
using GameServer.Models.Items;
using GameServer.Models.Tags;
using GameServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static GameServer.ChangeType;

namespace GameServer.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class AdminToolsController : BaseController
    {
        public AdminToolsController(IRepositoryManager repositoryManager, IApiLogger logger, SessionService sessionService)
            : base(repositoryManager, logger, sessionService) { }

        [SessionAuthorize]
        [HttpPost]
        public async Task<ApiResponse> AddEditItemAttributes([FromBody] AddEditItemAttributesData changeData)
        {
            var item = await Repositories.Items.GetItemAsync(changeData.ItemId);
            if (item is null)
            {
                return Error("Item does not exist.");
            }

            foreach (var change in changeData.Changes)
            {
                if (change.ChangeType == Add)
                {
                    item.ItemAttributes.Add(new GameCore.Entities.ItemAttribute()
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

            await Repositories.SaveChangesAsync();
            return Success();
        }

        [SessionAuthorize]
        [HttpPost]
        public async Task<ApiResponse> AddEditItemModAttributes([FromBody] AddEditItemModAttributesData changeData)
        {
            var itemMod = await Repositories.ItemMods.GetItemModAsync(changeData.ItemModId);
            if (itemMod is null)
            {
                return Error("Item Mod does not exist.");
            }

            foreach (var change in changeData.Changes)
            {
                if (change.ChangeType == Add)
                {
                    itemMod.ItemModAttributes.Add(new GameCore.Entities.ItemModAttribute()
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

            await Repositories.SaveChangesAsync();
            return Success();
        }

        [SessionAuthorize]
        [HttpPost]
        public async Task<ApiResponse> AddEditItemMods([FromBody] List<Change<ItemMod>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == Add)
                {
                    Repositories.Insert(new ItemMod
                    {
                        Name = change.Item.Name,
                        Removable = change.Item.Removable,
                        Description = change.Item.Description,
                        SlotTypeId = change.Item.SlotTypeId,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var itemMod = await Repositories.ItemMods.GetItemModAsync(change.Item.Id);
                    if (itemMod is not null)
                    {
                        itemMod.Name = change.Item.Name;
                        itemMod.Removable = change.Item.Removable;
                        itemMod.Description = change.Item.Description;
                        itemMod.SlotTypeId = change.Item.SlotTypeId;
                        Repositories.Update(itemMod);
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var itemMod = await Repositories.ItemMods.GetItemModAsync(change.Item.Id);
                    if (itemMod is not null)
                    {
                        Repositories.Delete(itemMod);
                    }
                }
            }

            await Repositories.SaveChangesAsync();
            return Success();
        }

        [SessionAuthorize]
        [HttpPost]
        public async Task<ApiResponse> AddEditItems([FromBody] List<Change<Item>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == Add)
                {
                    Repositories.Insert(new GameCore.Entities.Item
                    {
                        Name = change.Item.Name,
                        Description = change.Item.Description,
                        ItemCategoryId = change.Item.ItemCategoryId,
                        IconPath = change.Item.IconPath,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var item = await Repositories.Items.GetItemAsync(change.Item.Id);
                    if (item is not null)
                    {
                        item.Name = change.Item.Name;
                        item.Description = change.Item.Description;
                        item.ItemCategoryId = change.Item.ItemCategoryId;
                        item.IconPath = change.Item.IconPath;
                        Repositories.Update(item);
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var item = await Repositories.Items.GetItemAsync(change.Item.Id);
                    if (item is not null)
                    {
                        Repositories.Delete(item);
                    }
                }
            }

            await Repositories.SaveChangesAsync();
            return Success();
        }

        [SessionAuthorize]
        [HttpPost]
        public async Task<ApiResponse> AddEditItemSlots([FromBody] List<Change<ItemSlot>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == Add)
                {
                    Repositories.Insert(new GameCore.Entities.ItemSlot
                    {
                        ItemId = change.Item.ItemId,
                        SlotTypeId = change.Item.SlotTypeId,
                        GuaranteedItemModId = change.Item.GuaranteedItemModId,
                        Probability = change.Item.Probability,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    Repositories.Update(new GameCore.Entities.ItemSlot
                    {
                        Id = change.Item.Id,
                        ItemId = change.Item.ItemId,
                        SlotTypeId = change.Item.SlotTypeId,
                        GuaranteedItemModId = change.Item.GuaranteedItemModId,
                        Probability = change.Item.Probability,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    Repositories.Delete(new GameCore.Entities.ItemSlot
                    {
                        Id = change.Item.Id,
                    });
                }
            }

            await Repositories.SaveChangesAsync();
            return Success();
        }

        [SessionAuthorize]
        [HttpPost]
        public async Task<ApiResponse> AddEditTags([FromBody] List<Change<Tag>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == Add)
                {
                    Repositories.Insert(new GameCore.Entities.Tag
                    {
                        Name = change.Item.Name,
                        TagCategoryId = change.Item.TagCategoryId,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    Repositories.Update(new GameCore.Entities.Tag
                    {
                        Id = change.Item.Id,
                        Name = change.Item.Name,
                        TagCategoryId = change.Item.TagCategoryId,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    Repositories.Delete(new GameCore.Entities.Tag
                    {
                        Id = change.Item.Id,
                    });
                }
            }

            await Repositories.SaveChangesAsync();
            return Success();
        }

        [SessionAuthorize]
        [HttpPost]
        public async Task<ApiResponse> SetTagsForItem([FromBody] SetTagsData setTagsData)
        {
            var item = await Repositories.Items.GetItemAsync(setTagsData.Id);
            if (item is not null)
            {
                var tags = await Repositories.Tags.AllTags().Where(t => setTagsData.TagIds.Contains(t.Id)).ToListAsync();
                item.Tags = tags;
            }

            await Repositories.SaveChangesAsync();
            return Success();
        }

        [SessionAuthorize]
        [HttpPost]
        public async Task<ApiResponse> SetTagsForItemMod([FromBody] SetTagsData setTagsData)
        {
            var itemMod = await Repositories.ItemMods.GetItemModAsync(setTagsData.Id);
            if (itemMod is not null)
            {
                var tags = await Repositories.Tags.AllTags().Where(t => setTagsData.TagIds.Contains(t.Id)).ToListAsync();
                itemMod.Tags = tags;
            }

            await Repositories.SaveChangesAsync();
            return Success();

        }
    }
}
