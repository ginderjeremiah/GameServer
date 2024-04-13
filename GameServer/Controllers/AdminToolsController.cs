using DataAccess;
using GameLibrary.Logging;
using GameServer.Auth;
using GameServer.Models.Common;
using GameServer.Models.Items;
using GameServer.Models.Tags;
using Microsoft.AspNetCore.Mvc;
using static GameServer.ChangeType;

namespace GameServer.Controllers
{
    [SessionAuthorize]
    [Route("/api/[controller]/[action]")]
    [ApiController]
    public class AdminToolsController : BaseController
    {
        public AdminToolsController(IRepositoryManager repositoryManager, IApiLogger logger)
            : base(repositoryManager, logger) { }

        [HttpPost]
        public ApiResponse AddEditItemAttributes([FromBody] AddEditItemAttributesData changeData)
        {
            foreach (var change in changeData.Changes)
            {
                var item = change.Item;
                if (change.ChangeType == Add)
                {
                    Repositories.ItemAttributes.AddItemAttribute(changeData.ItemId, (int)item.AttributeId, item.Amount);
                }
                else if (change.ChangeType == Edit)
                {
                    Repositories.ItemAttributes.UpdateItemAttribute(changeData.ItemId, (int)item.AttributeId, item.Amount);
                }
                else if (change.ChangeType == Delete)
                {
                    Repositories.ItemAttributes.DeleteItemAttribute(changeData.ItemId, (int)item.AttributeId);
                }
            }

            return Success();
        }

        [HttpPost]
        public ApiResponse AddEditItemModAttributes([FromBody] AddEditItemModAttributesData changeData)
        {
            foreach (var change in changeData.Changes)
            {
                var item = change.Item;
                if (change.ChangeType == Add)
                {
                    Repositories.ItemModAttributes.AddItemModAttribute(changeData.ItemModId, (int)item.AttributeId, item.Amount);
                }
                else if (change.ChangeType == Edit)
                {
                    Repositories.ItemModAttributes.UpdateItemModAttribute(changeData.ItemModId, (int)item.AttributeId, item.Amount);
                }
                else if (change.ChangeType == Delete)
                {
                    Repositories.ItemModAttributes.DeleteItemModAttribute(changeData.ItemModId, (int)item.AttributeId);
                }
            }

            return Success();
        }

        [HttpPost]
        public ApiResponse AddEditItemMods([FromBody] List<Change<ItemMod>> changes)
        {
            foreach (var change in changes)
            {
                var item = change.Item;
                if (change.ChangeType == Add)
                {
                    Repositories.ItemMods.AddItemMod(item.ItemModName, item.Removable, item.ItemModDesc);
                }
                else if (change.ChangeType == Edit)
                {
                    Repositories.ItemMods.UpdateItemMod(item.ItemModId, item.ItemModName, item.Removable, item.ItemModDesc);
                }
                else if (change.ChangeType == Delete)
                {
                    Repositories.ItemMods.DeleteItemMod(item.ItemModId);
                }
            }
            return Success();

        }

        [HttpPost]
        public ApiResponse AddEditItems([FromBody] List<Change<Item>> changes)
        {
            foreach (var change in changes)
            {
                var item = change.Item;
                if (change.ChangeType == Add)
                {
                    Repositories.Items.AddItem(item.ItemName, item.ItemDesc, item.ItemCategoryId);
                }
                else if (change.ChangeType == Edit)
                {
                    Repositories.Items.UpdateItem(item.ItemId, item.ItemName, item.ItemDesc, item.ItemCategoryId);
                }
                else if (change.ChangeType == Delete)
                {
                    Repositories.Items.DeleteItem(item.ItemId);
                }
            }
            return Success();
        }

        [HttpPost]
        public ApiResponse AddEditItemSlots([FromBody] List<Change<ItemSlot>> changes)
        {
            foreach (var change in changes)
            {
                var item = change.Item;
                if (change.ChangeType == Add)
                {
                    Repositories.ItemSlots.AddItemSlot(item.ItemId, item.SlotTypeId, item.GuaranteedId, item.Probability);
                }
                else if (change.ChangeType == Edit)
                {
                    Repositories.ItemSlots.UpdateItemSlot(item.ItemSlotId, item.ItemId, item.SlotTypeId, item.GuaranteedId, item.Probability);
                }
                else if (change.ChangeType == Delete)
                {
                    Repositories.ItemSlots.DeleteItemSlot(item.ItemSlotId);
                }
            }

            return Success();
        }

        [HttpPost]
        public ApiResponse AddEditTags([FromBody] List<Change<Tag>> changes)
        {
            foreach (var change in changes)
            {
                var item = change.Item;
                if (change.ChangeType == Add)
                {
                    Repositories.Tags.AddTag(item.TagName, item.TagCategoryId);
                }
                else if (change.ChangeType == Edit)
                {
                    Repositories.Tags.UpdateTag(item.TagId, item.TagName, item.TagCategoryId);
                }
                else if (change.ChangeType == Delete)
                {
                    Repositories.Tags.DeleteTag(item.TagId);
                }
            }
            return Success();
        }

        [HttpPost]
        public ApiResponse SetTagsForItem([FromBody] SetTagsData setTagsData)
        {
            Repositories.Tags.SetItemTags(setTagsData.Id, setTagsData.TagIds);
            return Success();
        }

        [HttpPost]
        public ApiResponse SetTagsForItemMod([FromBody] SetTagsData setTagsData)
        {
            Repositories.Tags.SetItemModTags(setTagsData.Id, setTagsData.TagIds);
            return Success();

        }
    }
}
