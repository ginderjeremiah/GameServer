using Game.Api.CodeGen;
using Game.Api.CodeGen.Data;

namespace Game.Api.Tests.CodeGen
{
    [TestClass]
    public class EndpointMetadataTests
    {
        [TestMethod]
        public void Constructor_SimpleResponse_ExtractsType()
        {
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var metadata = new EndpointMetadata(method);

            Assert.IsNotNull(metadata.ResponseDescriptor);
            Assert.AreEqual(typeof(SimpleModel), metadata.ResponseDescriptor.UnderlyingType);
        }

        [TestMethod]
        public void Constructor_TaskWrappedResponse_UnwrapsTask()
        {
            var method = typeof(TestController).GetMethod("AsyncEndpoint")!;
            var metadata = new EndpointMetadata(method);

            Assert.IsNotNull(metadata.ResponseDescriptor);
            Assert.AreEqual(typeof(SimpleModel), metadata.ResponseDescriptor.UnderlyingType);
        }

        [TestMethod]
        public void Constructor_CollectionResponse_WrapsInList()
        {
            var method = typeof(TestController).GetMethod("GetList")!;
            var metadata = new EndpointMetadata(method);

            Assert.IsNotNull(metadata.ResponseDescriptor);
            Assert.AreEqual(typeof(List<SimpleModel>), metadata.ResponseDescriptor.UnderlyingType);
            Assert.IsTrue(metadata.ResponseDescriptor.UnderlyingType.IsEnumerable());
        }

        [TestMethod]
        public void Constructor_VoidResponse_NullDescriptor()
        {
            var method = typeof(TestController).GetMethod("PostData")!;
            var metadata = new EndpointMetadata(method);

            // ApiResponse (non-generic) → not a constructed generic type → null
            Assert.IsNull(metadata.ResponseDescriptor);
        }

        [TestMethod]
        public void Constructor_WithParameters_ExtractsAll()
        {
            var method = typeof(MultiParamController).GetMethod("UpdateMultiple")!;
            var metadata = new EndpointMetadata(method);

            Assert.AreEqual(2, metadata.ParameterDescriptors.Count);
            Assert.AreEqual("id", metadata.ParameterDescriptors[0].Name);
            Assert.AreEqual("name", metadata.ParameterDescriptors[1].Name);
        }

        [TestMethod]
        public void Constructor_NoParameters_EmptyList()
        {
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var metadata = new EndpointMetadata(method);

            Assert.AreEqual(0, metadata.ParameterDescriptors.Count);
        }

        [TestMethod]
        public void Constructor_SingleClassParameter_ExtractsCorrectly()
        {
            var method = typeof(TestController).GetMethod("PostData")!;
            var metadata = new EndpointMetadata(method);

            Assert.AreEqual(1, metadata.ParameterDescriptors.Count);
            Assert.AreEqual(typeof(SimpleModel), metadata.ParameterDescriptors[0].UnderlyingType);
            Assert.AreEqual("model", metadata.ParameterDescriptors[0].Name);
        }
    }
}
