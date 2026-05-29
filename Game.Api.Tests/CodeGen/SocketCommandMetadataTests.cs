using Game.Api.CodeGen.Data;
using Game.Api.Models.Common;
using Game.Api.Sockets;
using Game.Api.Sockets.Commands;
using Xunit;

namespace Game.Api.Tests.CodeGen
{
    public class SocketCommandMetadataTests
    {
        [Fact]
        public void Constructor_SetsCommandName()
        {
            var metadata = new SocketCommandMetadata(typeof(TestSocketCommandWithResponse));

            Assert.Equal("TestSocketCommandWithResponse", metadata.CommandName);
        }

        [Fact]
        public void Constructor_WithResponseData_SetsResponseDescriptor()
        {
            var metadata = new SocketCommandMetadata(typeof(TestSocketCommandWithResponse));

            Assert.NotNull(metadata.ResponseDescriptor);
            Assert.Equal(typeof(SimpleModel), metadata.ResponseDescriptor.UnderlyingType);
        }

        [Fact]
        public void Constructor_WithParameters_SetsParameterDescriptor()
        {
            var metadata = new SocketCommandMetadata(typeof(TestSocketCommandWithParams));

            Assert.NotNull(metadata.ParameterDescriptor);
            Assert.Equal(typeof(SocketParamModel), metadata.ParameterDescriptor.UnderlyingType);
        }

        [Fact]
        public void Constructor_WithBoth_SetsBothDescriptors()
        {
            var metadata = new SocketCommandMetadata(typeof(TestSocketCommandFull));

            Assert.NotNull(metadata.ResponseDescriptor);
            Assert.NotNull(metadata.ParameterDescriptor);
            Assert.Equal(typeof(SimpleModel), metadata.ResponseDescriptor.UnderlyingType);
            Assert.Equal(typeof(SocketParamModel), metadata.ParameterDescriptor.UnderlyingType);
        }

        [Fact]
        public void Constructor_NoResponseNoParams_BothNull()
        {
            var metadata = new SocketCommandMetadata(typeof(TestSocketCommandBasic));

            Assert.Null(metadata.ResponseDescriptor);
            Assert.Null(metadata.ParameterDescriptor);
        }
    }

    public class SocketParamModel
    {
        public int Id { get; set; }
        public string Value { get; set; } = "";
    }

    public class TestSocketCommandWithResponse : AbstractSocketCommandWithResponseData<SimpleModel>
    {
        public override string Name { get; set; } = "TestWithResponse";

        public override ApiSocketResponse<SimpleModel> HandleExecute(SocketContext context)
        {
            return Success(new SimpleModel());
        }
    }

    public class TestSocketCommandWithParams : AbstractSocketCommandWithParams<SocketParamModel>
    {
        public override string Name { get; set; } = "TestWithParams";

        public override ApiSocketResponse Execute(SocketContext context)
        {
            return Success();
        }
    }

    public class TestSocketCommandFull : AbstractSocketCommand<SimpleModel, SocketParamModel>
    {
        public override string Name { get; set; } = "TestFull";

        public override ApiSocketResponse<SimpleModel> HandleExecute(SocketContext context)
        {
            return Success(new SimpleModel());
        }
    }

    public class TestSocketCommandBasic : AbstractSocketCommand
    {
        public override string Name { get; set; } = "TestBasic";

        public override ApiSocketResponse Execute(SocketContext context)
        {
            return Success();
        }
    }
}
