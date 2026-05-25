using Game.Api.CodeGen.Data;

namespace Game.Api.Tests.CodeGen
{
    [TestClass]
    public class ControllerMetadataExtractorTests
    {
        [TestMethod]
        public void ExtractsEndpoints_FromController()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));

            Assert.AreEqual(4, extractor.Endpoints.Count);
        }

        [TestMethod]
        public void Endpoint_WithControllerRoute_ResolvesCorrectly()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var getSimple = extractor.Endpoints.First(e => e.Endpoint.Contains("Test"));

            // [HttpGet("/api/[controller]")] overrides the class-level route
            // Route: /api/Test → after trimming /api/ prefix → "Test"
            Assert.AreEqual("Test", getSimple.Endpoint);
        }

        [TestMethod]
        public void Endpoint_HttpPost_SetsIsGetFalse()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var postData = extractor.Endpoints.First(e => e.Endpoint.Contains("PostData"));

            Assert.IsFalse(postData.IsGet);
        }

        [TestMethod]
        public void Endpoint_HttpGet_SetsIsGetTrue()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var getSimple = extractor.Endpoints.First(e => e.Endpoint == "Test");

            Assert.IsTrue(getSimple.IsGet);
        }

        [TestMethod]
        public void Endpoint_WithMethodRouteOverride_UsesMethodRoute()
        {
            var extractor = new ControllerMetadataExtractor(typeof(CustomRouteController));
            var save = extractor.Endpoints.First(e => e.Endpoint.Contains("override"));

            // [HttpPost("/api/override/path")] → "override/path"
            Assert.AreEqual("override/path", save.Endpoint);
        }

        [TestMethod]
        public void Endpoint_CustomControllerRoute_ResolvesAction()
        {
            var extractor = new ControllerMetadataExtractor(typeof(CustomRouteController));
            var items = extractor.Endpoints.First(e => e.Endpoint.Contains("Items"));

            // Route "api/custom/[action]" → "custom/Items"
            Assert.AreEqual("custom/Items", items.Endpoint);
        }

        [TestMethod]
        public void Endpoint_WithResponseType_ExtractsDescriptor()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var getSimple = extractor.Endpoints.First(e => e.Endpoint == "Test");

            Assert.IsNotNull(getSimple.ResponseDescriptor);
            Assert.AreEqual(typeof(SimpleModel), getSimple.ResponseDescriptor.UnderlyingType);
        }

        [TestMethod]
        public void Endpoint_CollectionResponse_ExtractsListType()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var getList = extractor.Endpoints.First(e => e.Endpoint.Contains("GetList"));

            Assert.IsNotNull(getList.ResponseDescriptor);
            // IApiCollectionResponse triggers List<T> wrapping
            Assert.AreEqual(typeof(List<SimpleModel>), getList.ResponseDescriptor.UnderlyingType);
        }

        [TestMethod]
        public void Endpoint_NoReturnData_HasNullResponseDescriptor()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var postData = extractor.Endpoints.First(e => e.Endpoint.Contains("PostData"));

            // ApiResponse (non-generic) → no response descriptor since it's not a constructed generic
            Assert.IsNull(postData.ResponseDescriptor);
        }

        [TestMethod]
        public void Endpoint_WithParameter_ExtractsParameterDescriptor()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var postData = extractor.Endpoints.First(e => e.Endpoint.Contains("PostData"));

            Assert.AreEqual(1, postData.ParameterDescriptors.Count);
            Assert.AreEqual(typeof(SimpleModel), postData.ParameterDescriptors[0].UnderlyingType);
            Assert.AreEqual("model", postData.ParameterDescriptors[0].Name);
        }

        [TestMethod]
        public void Endpoint_AsyncMethod_UnwrapsTask()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var async = extractor.Endpoints.First(e => e.Endpoint.Contains("AsyncEndpoint"));

            // Task<ApiResponse<SimpleModel>> → unwraps Task, then ApiResponse<T> → SimpleModel
            Assert.IsNotNull(async.ResponseDescriptor);
            Assert.AreEqual(typeof(SimpleModel), async.ResponseDescriptor.UnderlyingType);
        }

        [TestMethod]
        public void Endpoint_MultipleParameters_ExtractsAll()
        {
            var extractor = new ControllerMetadataExtractor(typeof(MultiParamController));
            var update = extractor.Endpoints.First(e => e.Endpoint.Contains("UpdateMultiple"));

            Assert.AreEqual(2, update.ParameterDescriptors.Count);
            Assert.AreEqual("id", update.ParameterDescriptors[0].Name);
            Assert.AreEqual(typeof(int), update.ParameterDescriptors[0].UnderlyingType);
            Assert.AreEqual("name", update.ParameterDescriptors[1].Name);
            Assert.AreEqual(typeof(string), update.ParameterDescriptors[1].UnderlyingType);
        }

        [TestMethod]
        public void ExcludesNonActionMethods()
        {
            var extractor = new ControllerMetadataExtractor(typeof(ControllerWithNonAction));
            // Should not include the NonAction method, and should not include Dispose/ToString/etc.
            Assert.AreEqual(1, extractor.Endpoints.Count);
        }

        [TestMethod]
        public void ExcludesMethodsWithoutApiResponseReturn()
        {
            var extractor = new ControllerMetadataExtractor(typeof(ControllerWithMixedReturns));
            // Only includes methods that return IApiResponse or Task<IApiResponse>
            Assert.AreEqual(1, extractor.Endpoints.Count);
            Assert.IsTrue(extractor.Endpoints[0].Endpoint.Contains("Valid"));
        }
    }
}
