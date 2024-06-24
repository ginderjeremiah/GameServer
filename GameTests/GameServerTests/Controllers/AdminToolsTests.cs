using GameCore;
using GameCore.Entities;
using GameCore.Entities.ItemMods;
using GameCore.Entities.Items;
using GameServer.Models.Common;
using GameServer.Models.Items;
using GameServer.Models.Tags;
using GameTests.Mocks.DataAccess.Repositories;
using GameTests.Mocks.GameServer;
using System.Net.Http.Json;
using static GameCore.BattleSimulation.AttributeType;
using static GameServer.ChangeType;
using static System.Net.HttpStatusCode;
using BattlerAttribute = GameServer.Models.Attributes.BattlerAttribute;
using Item = GameCore.Entities.Item;
using ItemMod = GameCore.Entities.ItemMod;
using ItemModel = GameServer.Models.Items.Item;
using ItemModModel = GameServer.Models.Items.ItemMod;
using ItemSlot = GameCore.Entities.ItemSlot;
using ItemSlotModel = GameServer.Models.Items.ItemSlot;
using Tag = GameCore.Entities.Tag;
using TagModel = GameServer.Models.Tags.Tag;

namespace GameTests.GameServerTests.Controllers
{
    [TestClass]
    public class AdminToolsTests
    {
        [TestMethod]
        public async Task AddEditItemAttributes_AllEditTypes_ExecutesSuccessfully()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var atts = ((MockItemAttributes)app.Repositories.ItemAttributes).ItemAttributes;
            atts.Add(new ItemAttribute { ItemId = 1, AttributeId = (int)Intellect, Amount = 3.0m });
            atts.Add(new ItemAttribute { ItemId = 2, AttributeId = (int)Intellect, Amount = 10.0m });
            atts.Add(new ItemAttribute { ItemId = 1, AttributeId = (int)MaxHealth, Amount = 20.0m });
            atts.Add(new ItemAttribute { ItemId = 2, AttributeId = (int)MaxHealth, Amount = 30.0m });
            var payload = new AddEditItemAttributesData
            {
                ItemId = 1,
                Changes = new List<Change<BattlerAttribute>>
                {
                    new Change<BattlerAttribute>() { ChangeType = Add, Item = new BattlerAttribute { AttributeId = Agility, Amount = 1.0m } },
                    new Change<BattlerAttribute>() { ChangeType = Delete, Item = new BattlerAttribute { AttributeId = MaxHealth,} },
                    new Change<BattlerAttribute>() { ChangeType = Edit, Item = new BattlerAttribute { AttributeId = Intellect, Amount = 5.0m } }
                }
            };

            var response = await client.PostAsJsonAsync("/api/AdminTools/AddEditItemAttributes", payload);

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();
            atts = ((MockItemAttributes)app.Repositories.ItemAttributes).ItemAttributes;

            Assert.IsNotNull(data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(4, atts.Count);

            Assert.IsTrue(atts.Any(att => att.ItemId == 1 && att.AttributeId == (int)Agility));
            Assert.IsFalse(atts.Any(att => att.ItemId == 2 && att.AttributeId == (int)Agility));

            Assert.IsFalse(atts.Any(att => att.ItemId == 1 && att.AttributeId == (int)MaxHealth));
            Assert.IsTrue(atts.Any(att => att.ItemId == 2 && att.AttributeId == (int)MaxHealth));

            Assert.AreEqual(5.0m, atts.FirstOrDefault(att => att.ItemId == 1 && att.AttributeId == (int)Intellect)?.Amount);
            Assert.AreEqual(10.0m, atts.FirstOrDefault(att => att.ItemId == 2 && att.AttributeId == (int)Intellect)?.Amount);
        }

        [TestMethod]
        public async Task AddEditItemAttributes_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var payload = new AddEditItemAttributesData();

            var response = await client.PostAsJsonAsync("/api/AdminTools/AddEditItemAttributes", payload);

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task AddEditItemModAttributes_AllEditTypes_ExecutesSuccessfully()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var atts = ((MockItemModAttributes)app.Repositories.ItemModAttributes).ItemModAttributes;
            atts.Add(new ItemModAttribute { ItemModId = 1, AttributeId = (int)Intellect, Amount = 3.0m });
            atts.Add(new ItemModAttribute { ItemModId = 2, AttributeId = (int)Intellect, Amount = 10.0m });
            atts.Add(new ItemModAttribute { ItemModId = 1, AttributeId = (int)MaxHealth, Amount = 20.0m });
            atts.Add(new ItemModAttribute { ItemModId = 2, AttributeId = (int)MaxHealth, Amount = 30.0m });
            var payload = new AddEditItemModAttributesData
            {
                ItemModId = 1,
                Changes = new List<Change<BattlerAttribute>>
                {
                    new Change<BattlerAttribute>() { ChangeType = Add, Item = new BattlerAttribute { AttributeId = Agility, Amount = 1.0m } },
                    new Change<BattlerAttribute>() { ChangeType = Delete, Item = new BattlerAttribute { AttributeId = MaxHealth,} },
                    new Change<BattlerAttribute>() { ChangeType = Edit, Item = new BattlerAttribute { AttributeId = Intellect, Amount = 5.0m } }
                }
            };

            var response = await client.PostAsJsonAsync("/api/AdminTools/AddEditItemModAttributes", payload);

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNotNull(data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(4, atts.Count);

            Assert.IsTrue(atts.Any(att => att.ItemModId == 1 && att.AttributeId == (int)Agility));
            Assert.IsFalse(atts.Any(att => att.ItemModId == 2 && att.AttributeId == (int)Agility));

            Assert.IsFalse(atts.Any(att => att.ItemModId == 1 && att.AttributeId == (int)MaxHealth));
            Assert.IsTrue(atts.Any(att => att.ItemModId == 2 && att.AttributeId == (int)MaxHealth));

            Assert.AreEqual(5.0m, atts.FirstOrDefault(att => att.ItemModId == 1 && att.AttributeId == (int)Intellect)?.Amount);
            Assert.AreEqual(10.0m, atts.FirstOrDefault(att => att.ItemModId == 2 && att.AttributeId == (int)Intellect)?.Amount);
        }

        [TestMethod]
        public async Task AddEditItemModAttributes_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var payload = new AddEditItemAttributesData();

            var response = await client.PostAsJsonAsync("/api/AdminTools/AddEditItemModAttributes", payload);

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task AddEditItemMods_AllEditTypes_ExecutesSuccessfully()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var mods = ((MockItemMods)app.Repositories.ItemMods).ItemMods;
            mods.Add(new ItemMod { ItemModId = 1, ItemModName = "Test1", ItemModDesc = "Test1", Removable = true, SlotTypeId = 1 });
            mods.Add(new ItemMod { ItemModId = 2, ItemModName = "Test2", ItemModDesc = "Test2", Removable = false, SlotTypeId = 2 });
            mods.Add(new ItemMod { ItemModId = 3, ItemModName = "Test3", ItemModDesc = "Test3", Removable = true, SlotTypeId = 3 });
            var payload = new List<Change<ItemModModel>>
            {
                new Change<ItemModModel>() { ChangeType = Add, Item = new ItemModModel { ItemModId = 1, ItemModName = "Test4", ItemModDesc = "Test4", Removable = true, SlotTypeId = 4, Attributes = new()} },
                new Change<ItemModModel>() { ChangeType = Delete, Item = new ItemModModel { ItemModId = 1, ItemModName = "", ItemModDesc = "", Removable = false, SlotTypeId = 0, Attributes = new()} },
                new Change<ItemModModel>() { ChangeType = Edit, Item = new ItemModModel { ItemModId = 2, ItemModName = "Test22", ItemModDesc = "Test22", Removable = true, SlotTypeId = 22, Attributes = new()} }
            };

            var response = await client.PostAsJsonAsync("/api/AdminTools/AddEditItemMods", payload);

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNotNull(data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(3, mods.Count);

            Assert.IsFalse(mods.Any(mod => mod.ItemModId == 1));

            var changedMod = mods.FirstOrDefault(mod => mod.ItemModId == 2);
            Assert.IsNotNull(changedMod);
            Assert.AreEqual("Test22", changedMod.ItemModName);
            Assert.AreEqual("Test22", changedMod.ItemModDesc);
            Assert.AreEqual(true, changedMod.Removable);
            Assert.AreEqual(22, changedMod.SlotTypeId);

            Assert.IsNotNull(mods.FirstOrDefault(mod => mod.ItemModId == 4));
        }

        [TestMethod]
        public async Task AddEditItemMods_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var payload = new List<Change<ItemModModel>>();

            var response = await client.PostAsJsonAsync("/api/AdminTools/AddEditItemMods", payload);

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task AddEditItems_AllEditTypes_ExecutesSuccessfully()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var items = ((MockItems)app.Repositories.Items).Items;
            items.Add(new Item { ItemId = 1, ItemName = "Test1", ItemDesc = "Test1", ItemCategoryId = 1, IconPath = "Test1" });
            items.Add(new Item { ItemId = 2, ItemName = "Test2", ItemDesc = "Test2", ItemCategoryId = 2, IconPath = "Test2" });
            items.Add(new Item { ItemId = 3, ItemName = "Test3", ItemDesc = "Test3", ItemCategoryId = 3, IconPath = "Test3" });
            var payload = new List<Change<ItemModel>>
            {
                new Change<ItemModel>() { ChangeType = Add, Item = new ItemModel { ItemId = 1, ItemName = "Test4", ItemDesc = "Test4", ItemCategoryId = 4, IconPath = "Test4", Attributes = new()} },
                new Change<ItemModel>() { ChangeType = Delete, Item = new ItemModel { ItemId = 1, ItemName = "", ItemDesc = "", ItemCategoryId = 0, IconPath = "", Attributes = new()} },
                new Change<ItemModel>() { ChangeType = Edit, Item = new ItemModel { ItemId = 2, ItemName = "Test22", ItemDesc = "Test22", ItemCategoryId = 22, IconPath = "Test22", Attributes = new()} }
            };

            var response = await client.PostAsJsonAsync("/api/AdminTools/AddEditItems", payload);

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNotNull(data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(3, items.Count);

            Assert.IsFalse(items.Any(item => item.ItemId == 1));

            var changedItem = items.FirstOrDefault(item => item.ItemId == 2);
            Assert.IsNotNull(changedItem);
            Assert.AreEqual("Test22", changedItem.ItemName);
            Assert.AreEqual("Test22", changedItem.ItemDesc);
            Assert.AreEqual(22, changedItem.ItemCategoryId);
            Assert.AreEqual("Test22", changedItem.IconPath);

            Assert.IsNotNull(items.FirstOrDefault(item => item.ItemId == 4));
        }

        [TestMethod]
        public async Task AddEditItems_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var payload = new List<Change<ItemModel>>();

            var response = await client.PostAsJsonAsync("/api/AdminTools/AddEditItems", payload);

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task AddEditItemSlots_AllEditTypes_ExecutesSuccessfully()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var itemSlots = ((MockItemSlots)app.Repositories.ItemSlots).ItemSlots;
            itemSlots.Add(new ItemSlot { ItemSlotId = 1, ItemId = 1, SlotTypeId = 1, GuaranteedId = -1, Probability = 0.1m });
            itemSlots.Add(new ItemSlot { ItemSlotId = 2, ItemId = 2, SlotTypeId = 2, GuaranteedId = -1, Probability = 0.2m });
            itemSlots.Add(new ItemSlot { ItemSlotId = 3, ItemId = 3, SlotTypeId = 3, GuaranteedId = 1, Probability = 0.3m });
            var payload = new List<Change<ItemSlotModel>>
            {
                new Change<ItemSlotModel>() { ChangeType = Add, Item = new ItemSlotModel { ItemSlotId = -1, ItemId = 4, SlotTypeId = 4, GuaranteedId = 4, Probability = 0.4m } },
                new Change<ItemSlotModel>() { ChangeType = Delete, Item = new ItemSlotModel { ItemSlotId = 1, ItemId = 0, SlotTypeId = 0, GuaranteedId = 0, Probability = 0.0m } },
                new Change<ItemSlotModel>() { ChangeType = Edit, Item = new ItemSlotModel { ItemSlotId = 2, ItemId = 22, SlotTypeId = 22, GuaranteedId = 22, Probability = 0.22m } }
            };

            var response = await client.PostAsJsonAsync("/api/AdminTools/AddEditItemSlots", payload);

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNotNull(data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(3, itemSlots.Count);

            Assert.IsFalse(itemSlots.Any(slot => slot.ItemId == 1));

            var changedSlot = itemSlots.FirstOrDefault(slot => slot.ItemSlotId == 2);
            Assert.IsNotNull(changedSlot);
            Assert.AreEqual(22, changedSlot.ItemId);
            Assert.AreEqual(22, changedSlot.SlotTypeId);
            Assert.AreEqual(22, changedSlot.GuaranteedId);
            Assert.AreEqual(0.22m, changedSlot.Probability);

            Assert.IsNotNull(itemSlots.FirstOrDefault(slot => slot.ItemSlotId == 4));
        }

        [TestMethod]
        public async Task AddEditItemSlots_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var payload = new List<Change<ItemModel>>();

            var response = await client.PostAsJsonAsync("/api/AdminTools/AddEditItemSlots", payload);

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task AddEditTags_AllEditTypes_ExecutesSuccessfully()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var tags = ((MockTags)app.Repositories.Tags).Tags;
            tags.Add(new Tag { TagId = 1, TagName = "Test1", TagCategoryId = 1 });
            tags.Add(new Tag { TagId = 2, TagName = "Test2", TagCategoryId = 2 });
            tags.Add(new Tag { TagId = 3, TagName = "Test3", TagCategoryId = 3 });
            var payload = new List<Change<TagModel>>
            {
                new Change<TagModel>() { ChangeType = Add, Item = new TagModel { TagId = -1, TagName = "Test4", TagCategoryId = 4} },
                new Change<TagModel>() { ChangeType = Delete, Item = new TagModel { TagId = 1, TagName = "", TagCategoryId = 0 } },
                new Change<TagModel>() { ChangeType = Edit, Item = new TagModel { TagId = 2, TagName = "Test22", TagCategoryId = 22 } }
            };

            var response = await client.PostAsJsonAsync("/api/AdminTools/AddEditTags", payload);

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNotNull(data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(3, tags.Count);

            Assert.IsFalse(tags.Any(tag => tag.TagId == 1));

            var changedTag = tags.FirstOrDefault(tag => tag.TagId == 2);
            Assert.IsNotNull(changedTag);
            Assert.AreEqual("Test22", changedTag.TagName);
            Assert.AreEqual(22, changedTag.TagCategoryId);

            Assert.IsNotNull(tags.FirstOrDefault(tag => tag.TagId == 4));
        }

        [TestMethod]
        public async Task AddEditTags_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var payload = new List<Change<TagModel>>();

            var response = await client.PostAsJsonAsync("/api/AdminTools/AddEditTags", payload);

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task SetTagsForItem_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var payload = new SetTagsData();

            var response = await client.PostAsJsonAsync("/api/AdminTools/SetTagsForItem", payload);

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task SetTagsForItem_StandardRequest_ExecutesSuccessfully()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var mockTags = (MockTags)app.Repositories.Tags;
            mockTags.Tags.Add(new Tag { TagId = 1, TagName = "Test1", TagCategoryId = 1 });
            mockTags.Tags.Add(new Tag { TagId = 2, TagName = "Test2", TagCategoryId = 2 });
            mockTags.Tags.Add(new Tag { TagId = 3, TagName = "Test3", TagCategoryId = 3 });
            var payload = new SetTagsData
            {
                Id = 1,
                TagIds = new List<int> { 1, 2 }
            };

            var response = await client.PostAsJsonAsync("/api/AdminTools/SetTagsForItem", payload);

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNotNull(data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(3, mockTags.Tags.Count);
            Assert.AreEqual(2, mockTags.ItemTagIds[1].Count);
            Assert.IsTrue(mockTags.TagsForItem(1).All(tag => tag.TagId is 1 or 2));
        }

        [TestMethod]
        public async Task SetTagsForItemMod_NoSession_ReturnsForbidden()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var payload = new SetTagsData();

            var response = await client.PostAsJsonAsync("/api/AdminTools/SetTagsForItemMod", payload);

            Assert.AreEqual(Forbidden, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNull(data);
        }

        [TestMethod]
        public async Task SetTagsForItemMod_StandardRequest_ExecutesSuccessfully()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            app.AddAuthorizedSession(client);
            var mockTags = (MockTags)app.Repositories.Tags;
            mockTags.Tags.Add(new Tag { TagId = 1, TagName = "Test1", TagCategoryId = 1 });
            mockTags.Tags.Add(new Tag { TagId = 2, TagName = "Test2", TagCategoryId = 2 });
            mockTags.Tags.Add(new Tag { TagId = 3, TagName = "Test3", TagCategoryId = 3 });
            var payload = new SetTagsData
            {
                Id = 1,
                TagIds = new List<int> { 1, 2 }
            };

            var response = await client.PostAsJsonAsync("/api/AdminTools/SetTagsForItemMod", payload);

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiResponse>();

            Assert.IsNotNull(data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(3, mockTags.Tags.Count);
            Assert.AreEqual(2, mockTags.ItemModTagIds[1].Count);
            Assert.IsTrue(mockTags.TagsForItemMod(1).All(tag => tag.TagId is 1 or 2));
        }
    }
}