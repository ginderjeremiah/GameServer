using Game.Api.CodeGen.Data;
using Xunit;

namespace Game.Api.Tests.CodeGen
{
    public class ControllerMetadataExtractorTests
    {
        [Fact]
        public void ExtractsEndpoints_FromController()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));

            Assert.Equal(4, extractor.Endpoints.Count);
        }

        [Fact]
        public void Endpoint_WithControllerRoute_ResolvesCorrectly()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var getSimple = extractor.Endpoints.First(e => e.Endpoint.Contains("Test"));

            // [HttpGet("/api/[controller]")] overrides the class-level route
            // Route: /api/Test → after trimming /api/ prefix → "Test"
            Assert.Equal("Test", getSimple.Endpoint);
        }

        [Fact]
        public void Endpoint_HttpPost_SetsIsGetFalse()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var postData = extractor.Endpoints.First(e => e.Endpoint.Contains("PostData"));

            Assert.False(postData.IsGet);
        }

        [Fact]
        public void Endpoint_HttpGet_SetsIsGetTrue()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var getSimple = extractor.Endpoints.First(e => e.Endpoint == "Test");

            Assert.True(getSimple.IsGet);
        }

        [Fact]
        public void Endpoint_WithMethodRouteOverride_UsesMethodRoute()
        {
            var extractor = new ControllerMetadataExtractor(typeof(CustomRouteController));
            var save = extractor.Endpoints.First(e => e.Endpoint.Contains("override"));

            // [HttpPost("/api/override/path")] → "override/path"
            Assert.Equal("override/path", save.Endpoint);
        }

        [Fact]
        public void Endpoint_CustomControllerRoute_ResolvesAction()
        {
            var extractor = new ControllerMetadataExtractor(typeof(CustomRouteController));
            var items = extractor.Endpoints.First(e => e.Endpoint.Contains("Items"));

            // Route "api/custom/[action]" → "custom/Items"
            Assert.Equal("custom/Items", items.Endpoint);
        }

        [Fact]
        public void Endpoint_WithResponseType_ExtractsDescriptor()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var getSimple = extractor.Endpoints.First(e => e.Endpoint == "Test");

            Assert.NotNull(getSimple.ResponseDescriptor);
            Assert.Equal(typeof(SimpleModel), getSimple.ResponseDescriptor.UnderlyingType);
        }

        [Fact]
        public void Endpoint_CollectionResponse_ExtractsListType()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var getList = extractor.Endpoints.First(e => e.Endpoint.Contains("GetList"));

            Assert.NotNull(getList.ResponseDescriptor);
            // IApiCollectionResponse triggers List<T> wrapping
            Assert.Equal(typeof(List<SimpleModel>), getList.ResponseDescriptor.UnderlyingType);
        }

        [Fact]
        public void Endpoint_NoReturnData_HasNullResponseDescriptor()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var postData = extractor.Endpoints.First(e => e.Endpoint.Contains("PostData"));

            // ApiResponse (non-generic) → no response descriptor since it's not a constructed generic
            Assert.Null(postData.ResponseDescriptor);
        }

        [Fact]
        public void Endpoint_WithParameter_ExtractsParameterDescriptor()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var postData = extractor.Endpoints.First(e => e.Endpoint.Contains("PostData"));

            Assert.Single(postData.ParameterDescriptors);
            Assert.Equal(typeof(SimpleModel), postData.ParameterDescriptors[0].UnderlyingType);
            Assert.Equal("model", postData.ParameterDescriptors[0].Name);
        }

        [Fact]
        public void Endpoint_AsyncMethod_UnwrapsTask()
        {
            var extractor = new ControllerMetadataExtractor(typeof(TestController));
            var async = extractor.Endpoints.First(e => e.Endpoint.Contains("AsyncEndpoint"));

            // Task<ApiResponse<SimpleModel>> → unwraps Task, then ApiResponse<T> → SimpleModel
            Assert.NotNull(async.ResponseDescriptor);
            Assert.Equal(typeof(SimpleModel), async.ResponseDescriptor.UnderlyingType);
        }

        [Fact]
        public void Endpoint_MultipleParameters_ExtractsAll()
        {
            var extractor = new ControllerMetadataExtractor(typeof(MultiParamController));
            var update = extractor.Endpoints.First(e => e.Endpoint.Contains("UpdateMultiple"));

            Assert.Equal(2, update.ParameterDescriptors.Count);
            Assert.Equal("id", update.ParameterDescriptors[0].Name);
            Assert.Equal(typeof(int), update.ParameterDescriptors[0].UnderlyingType);
            Assert.Equal("name", update.ParameterDescriptors[1].Name);
            Assert.Equal(typeof(string), update.ParameterDescriptors[1].UnderlyingType);
        }

        [Fact]
        public void ExcludesNonActionMethods()
        {
            var extractor = new ControllerMetadataExtractor(typeof(ControllerWithNonAction));
            // Should not include the NonAction method, and should not include Dispose/ToString/etc.
            Assert.Single(extractor.Endpoints);
        }

        [Fact]
        public void ExcludesMethodsWithoutApiResponseReturn()
        {
            var extractor = new ControllerMetadataExtractor(typeof(ControllerWithMixedReturns));
            // Only includes methods that return IApiResponse or Task<IApiResponse>
            Assert.Single(extractor.Endpoints);
            Assert.Contains("Valid", extractor.Endpoints[0].Endpoint);
        }
    }
}
