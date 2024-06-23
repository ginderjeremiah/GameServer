using GameCore;
using GameServer.Models.Common;
using GameTests.Mocks.DataAccess.Repositories;
using GameTests.Mocks.GameServer;
using static System.Net.HttpStatusCode;
using Attribute = GameCore.Entities.Attributes.Attribute;
using AttributeModel = GameServer.Models.Attributes.Attribute;

namespace GameTests.GameServerTests.Controllers
{
    [TestClass]
    public class AttributesTests
    {
        [TestMethod]
        public async Task Attributes_StandardRequest_ReturnsValidData()
        {
            using var app = new ApiAppFactory();
            var client = app.CreateClient();
            var atts = ((MockAttributes)app.Repositories.Attributes).Attributes;
            atts.Add(new Attribute { AttributeId = 1, AttributeName = "Test", AttributeDesc = "TEST" });

            var response = await client.GetAsync("/api/Attributes");

            Assert.AreEqual(OK, response.StatusCode);

            var data = response.Deserialize<ApiListResponse<AttributeModel>>();

            Assert.IsNotNull(data?.Data);
            Assert.IsNull(data.Error);
            Assert.AreEqual(atts.Count, data.Data.Count);
            Assert.AreEqual(atts[0].AttributeId, (int)data.Data[0].AttributeId);
            Assert.AreEqual(atts[0].AttributeName, data.Data[0].AttributeName);
        }
    }
}