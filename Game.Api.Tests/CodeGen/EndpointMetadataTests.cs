using Game.Api.CodeGen;
using Game.Api.CodeGen.Data;
using Xunit;

namespace Game.Api.Tests.CodeGen
{
    public class EndpointMetadataTests
    {
        [Fact]
        public void Constructor_SimpleResponse_ExtractsType()
        {
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var metadata = new EndpointMetadata(method);

            Assert.NotNull(metadata.ResponseDescriptor);
            Assert.Equal(typeof(SimpleModel), metadata.ResponseDescriptor.UnderlyingType);
        }

        [Fact]
        public void Constructor_TaskWrappedResponse_UnwrapsTask()
        {
            var method = typeof(TestController).GetMethod("AsyncEndpoint")!;
            var metadata = new EndpointMetadata(method);

            Assert.NotNull(metadata.ResponseDescriptor);
            Assert.Equal(typeof(SimpleModel), metadata.ResponseDescriptor.UnderlyingType);
        }

        [Fact]
        public void Constructor_CollectionResponse_WrapsInList()
        {
            var method = typeof(TestController).GetMethod("GetList")!;
            var metadata = new EndpointMetadata(method);

            Assert.NotNull(metadata.ResponseDescriptor);
            Assert.Equal(typeof(List<SimpleModel>), metadata.ResponseDescriptor.UnderlyingType);
            Assert.True(metadata.ResponseDescriptor.UnderlyingType.IsEnumerable());
        }

        [Fact]
        public void Constructor_VoidResponse_NullDescriptor()
        {
            var method = typeof(TestController).GetMethod("PostData")!;
            var metadata = new EndpointMetadata(method);

            // ApiResponse (non-generic) → not a constructed generic type → null
            Assert.Null(metadata.ResponseDescriptor);
        }

        [Fact]
        public void Constructor_WithParameters_ExtractsAll()
        {
            var method = typeof(MultiParamController).GetMethod("UpdateMultiple")!;
            var metadata = new EndpointMetadata(method);

            Assert.Equal(2, metadata.ParameterDescriptors.Count);
            Assert.Equal("id", metadata.ParameterDescriptors[0].Name);
            Assert.Equal("name", metadata.ParameterDescriptors[1].Name);
        }

        [Fact]
        public void Constructor_NoParameters_EmptyList()
        {
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var metadata = new EndpointMetadata(method);

            Assert.Equal(0, metadata.ParameterDescriptors.Count);
        }

        [Fact]
        public void Constructor_SingleClassParameter_ExtractsCorrectly()
        {
            var method = typeof(TestController).GetMethod("PostData")!;
            var metadata = new EndpointMetadata(method);

            Assert.Equal(1, metadata.ParameterDescriptors.Count);
            Assert.Equal(typeof(SimpleModel), metadata.ParameterDescriptors[0].UnderlyingType);
            Assert.Equal("model", metadata.ParameterDescriptors[0].Name);
        }
    }
}
