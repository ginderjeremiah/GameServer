using GameCore;
using GameServer.Models.Common;
using GameTests.Mocks.DataAccess.Repositories;
using GameTests.Mocks.GameServer;
using static System.Net.HttpStatusCode;
using ItemCategory = GameCore.Entities.ItemCategories.ItemCategory;
using ItemCategoryModel = GameServer.Models.Items.ItemCategory;

namespace GameTests.GameServerTests.Controllers
{
    [TestClass]
    public class ItemCategoriesTests
    {
        [TestMethod]
        public async Task ItemCategories_StandardRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var categories = ((MockItemCategories)app.Repositories.ItemCategories).ItemCategories;
            categories.Add(new ItemCategory { ItemCategoryId = 1, CategoryName = "Test" });

            var response = await client.GetAsync("/api/ItemCategories");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiListResponse<ItemCategoryModel>>();

            Assert.IsNotNull(data?.Data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(categories.Count, data.Data.Count);
            Assert.AreEqual(categories[0].ItemCategoryId, data.Data[0].ItemCategoryId);
            Assert.AreEqual(categories[0].CategoryName, data.Data[0].CategoryName);
        }
    }
}