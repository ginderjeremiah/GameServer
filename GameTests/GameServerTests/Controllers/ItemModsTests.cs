using GameCore;
using GameServer.Models.Common;
using GameTests.Mocks.DataAccess.Repositories;
using GameTests.Mocks.GameServer;
using static System.Net.HttpStatusCode;
using ItemMod = GameCore.Entities.ItemMods.ItemMod;
using ItemModModel = GameServer.Models.Items.ItemMod;

namespace GameTests.GameServerTests.Controllers
{
    [TestClass]
    public class ItemModsTests
    {
        [TestMethod]
        public async Task ItemMods_RefreshRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var itemMods = (MockItemMods)app.Repositories.ItemMods;
            itemMods.ItemMods.Add(new ItemMod { ItemModId = 1, ItemModName = "Test", ItemModDesc = "Test", SlotTypeId = 1, Removable = true, Attributes = new() });
            var refresh = true;

            var response = await client.GetAsync($"/api/ItemMods?refreshCache={refresh}");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiListResponse<ItemModModel>>();

            Assert.IsNotNull(data?.Data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(itemMods.ItemMods.Count, data.Data.Count);
            Assert.AreEqual(itemMods.ItemMods[0].ItemModId, data.Data[0].ItemModId);
            Assert.AreEqual(itemMods.ItemMods[0].ItemModName, data.Data[0].ItemModName);
            Assert.AreEqual(refresh, itemMods.Refreshed);
        }

        [TestMethod]
        public async Task ItemMods_StandardRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var mods = ((MockItemMods)app.Repositories.ItemMods).ItemMods;
            mods.Add(new ItemMod { ItemModId = 1, ItemModName = "Test", ItemModDesc = "Test", SlotTypeId = 1, Removable = true, Attributes = new() });

            var response = await client.GetAsync("/api/ItemMods");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiListResponse<ItemModModel>>();

            Assert.IsNotNull(data?.Data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(mods.Count, data.Data.Count);
            Assert.AreEqual(mods[0].ItemModId, data.Data[0].ItemModId);
            Assert.AreEqual(mods[0].ItemModName, data.Data[0].ItemModName);
        }
    }
}