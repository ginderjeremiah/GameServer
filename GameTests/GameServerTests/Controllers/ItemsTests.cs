using GameCore;
using GameServer.Models.Common;
using GameTests.Mocks.DataAccess.Repositories;
using GameTests.Mocks.GameServer;
using static System.Net.HttpStatusCode;
using Item = GameCore.Entities.Item;
using ItemModel = GameServer.Models.Items.Item;
using ItemSlot = GameCore.Entities.ItemSlot;
using ItemSlotModel = GameServer.Models.Items.ItemSlot;
using SlotType = GameCore.Entities.SlotType;
using SlotTypeModel = GameServer.Models.Items.SlotType;

namespace GameTests.GameServerTests.Controllers
{
    [TestClass]
    public class ItemsTests
    {
        [TestMethod]
        public async Task Items_StandardRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var items = ((MockItems)app.Repositories.Items);
            items.Items.Add(new Item { ItemId = 1, ItemName = "Test", ItemDesc = "Test", ItemCategoryId = 1, IconPath = "Test", Attributes = new() });

            var response = await client.GetAsync("/api/Items");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiListResponse<ItemModel>>();

            Assert.IsNotNull(data?.Data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(false, items.Refreshed);
            Assert.AreEqual(items.Items.Count, data.Data.Count);
            Assert.AreEqual(items.Items[0].ItemId, data.Data[0].ItemId);
            Assert.AreEqual(items.Items[0].ItemName, data.Data[0].ItemName);
        }

        [TestMethod]
        public async Task Items_WithRefresh_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var items = ((MockItems)app.Repositories.Items);
            items.Items.Add(new Item { ItemId = 1, ItemName = "Test", ItemDesc = "Test", ItemCategoryId = 1, IconPath = "Test", Attributes = new() });
            var refreshCache = true;

            var response = await client.GetAsync($"/api/Items?refreshCache={refreshCache}");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiListResponse<ItemModel>>();

            Assert.IsNotNull(data?.Data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(refreshCache, items.Refreshed);
            Assert.AreEqual(items.Items.Count, data.Data.Count);
            Assert.AreEqual(items.Items[0].ItemId, data.Data[0].ItemId);
            Assert.AreEqual(items.Items[0].ItemName, data.Data[0].ItemName);
        }

        [TestMethod]
        public async Task SlotsForItem_BadParameters_ReturnsNoData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var itemSlots = ((MockItemSlots)app.Repositories.ItemSlots).ItemSlots;
            itemSlots.Add(new ItemSlot { ItemSlotId = 1, ItemId = 1, SlotTypeId = 1, GuaranteedId = 1, Probability = 0.25m });

            var response = await client.GetAsync("/api/Items/SlotsForItem?itemId=-1");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiListResponse<ItemModel>>();

            Assert.IsNotNull(data?.Data);
            Assert.AreEqual(0, data.Data.Count);
        }

        [TestMethod]
        public async Task SlotsForItem_StandardRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var itemSlots = ((MockItemSlots)app.Repositories.ItemSlots).ItemSlots;
            itemSlots.Add(new ItemSlot { ItemSlotId = 1, ItemId = 1, SlotTypeId = 1, GuaranteedId = 1, Probability = 0.25m });

            var response = await client.GetAsync("/api/Items/SlotsForItem?itemId=1");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiListResponse<ItemSlotModel>>();

            Assert.IsNotNull(data?.Data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(itemSlots.Count, data.Data.Count);
            Assert.AreEqual(itemSlots[0].ItemSlotId, data.Data[0].ItemSlotId);
            Assert.AreEqual(itemSlots[0].ItemId, data.Data[0].ItemId);
            Assert.AreEqual(itemSlots[0].SlotTypeId, data.Data[0].SlotTypeId);
            Assert.AreEqual(itemSlots[0].GuaranteedId, data.Data[0].GuaranteedId);
            Assert.AreEqual(itemSlots[0].Probability, data.Data[0].Probability);
        }

        [TestMethod]
        public async Task SlotsForItem_WithRefresh_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var itemSlots = ((MockItemSlots)app.Repositories.ItemSlots);
            itemSlots.ItemSlots.Add(new ItemSlot { ItemSlotId = 1, ItemId = 1, SlotTypeId = 1, GuaranteedId = 1, Probability = 0.25m });
            var refreshCache = true;

            var response = await client.GetAsync($"/api/Items/SlotsForItem?itemId=1&refreshCache={refreshCache}");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiListResponse<ItemSlotModel>>();

            Assert.IsNotNull(data?.Data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(refreshCache, itemSlots.Refreshed);
            Assert.AreEqual(itemSlots.ItemSlots[0].ItemSlotId, data.Data[0].ItemSlotId);
            Assert.AreEqual(itemSlots.ItemSlots[0].ItemId, data.Data[0].ItemId);
            Assert.AreEqual(itemSlots.ItemSlots[0].SlotTypeId, data.Data[0].SlotTypeId);
            Assert.AreEqual(itemSlots.ItemSlots[0].GuaranteedId, data.Data[0].GuaranteedId);
            Assert.AreEqual(itemSlots.ItemSlots[0].Probability, data.Data[0].Probability);
        }

        [TestMethod]
        public async Task SlotTypes_StandardRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var slotTypes = ((MockSlotTypes)app.Repositories.SlotTypes).SlotTypes;
            slotTypes.Add(new SlotType { SlotTypeId = 1, SlotTypeName = "Test" });

            var response = await client.GetAsync("/api/Items/SlotTypes");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiListResponse<SlotTypeModel>>();

            Assert.IsNotNull(data?.Data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(slotTypes.Count, data.Data.Count);
            Assert.AreEqual(slotTypes[0].SlotTypeId, data.Data[0].SlotTypeId);
            Assert.AreEqual(slotTypes[0].SlotTypeName, data.Data[0].SlotTypeName);
        }
    }
}