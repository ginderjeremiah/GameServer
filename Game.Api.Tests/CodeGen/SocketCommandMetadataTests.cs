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

        [Fact]
        public void Constructor_DerivedCommand_ResolvesGenericBaseFromChain()
        {
            var metadata = new SocketCommandMetadata(typeof(TestSocketCommandDerivedFull));

            Assert.NotNull(metadata.ResponseDescriptor);
            Assert.NotNull(metadata.ParameterDescriptor);
            Assert.Equal(typeof(SimpleModel), metadata.ResponseDescriptor.UnderlyingType);
            Assert.Equal(typeof(SocketParamModel), metadata.ParameterDescriptor.UnderlyingType);
        }

        [Fact]
        public void Constructor_DecoyMembers_NotExtractedWithoutGenericBase()
        {
            // Members named "Parameters"/"HandleExecute" that are not from the typed generic base must
            // not be mistaken for the real descriptors.
            var metadata = new SocketCommandMetadata(typeof(TestSocketCommandWithDecoyMembers));

            Assert.Null(metadata.ResponseDescriptor);
            Assert.Null(metadata.ParameterDescriptor);
        }

        [Fact]
        public void Constructor_ServerInitiatedCommand_OmitsParameterDescriptor()
        {
            // A server-initiated command may still bind Parameters from a typed base (to reuse
            // DeserializeParameters<T>'s malformed-payload classification), but the client only ever listens
            // for it and can never send it, so it must not surface in ApiSocketRequestTypes.
            var metadata = new SocketCommandMetadata(typeof(TestSocketCommandServerInitiatedFull));

            Assert.NotNull(metadata.ResponseDescriptor);
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

        public override Task<ApiSocketResponse<SimpleModel>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(Success(new SimpleModel()));
        }
    }

    public class TestSocketCommandWithParams : AbstractSocketCommandWithParams<SocketParamModel>
    {
        public override string Name { get; set; } = "TestWithParams";

        public override Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(Success());
        }
    }

    public class TestSocketCommandFull : AbstractSocketCommand<SimpleModel, SocketParamModel>
    {
        public override string Name { get; set; } = "TestFull";

        public override Task<ApiSocketResponse<SimpleModel>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(Success(new SimpleModel()));
        }
    }

    public class TestSocketCommandBasic : AbstractSocketCommand
    {
        public override string Name { get; set; } = "TestBasic";

        public override Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(Success());
        }
    }

    // A multi-level hierarchy: the response/parameter generic bases are not the direct base, so the
    // metadata must walk the base chain to find the closed generic base rather than only inspecting it.
    public class TestSocketCommandDerivedFull : TestSocketCommandFull
    {
        public override string Name { get; set; } = "TestDerivedFull";
    }

    // A basic command (no params/response generic base) that nonetheless declares members named
    // "Parameters" and "HandleExecuteAsync". Resolving by raw member name would mis-extract these; gating on
    // the typed generic base means neither descriptor is set.
    public class TestSocketCommandWithDecoyMembers : AbstractSocketCommand
    {
        public override string Name { get; set; } = "TestDecoy";

        public string Parameters { get; set; } = "";

        public int HandleExecuteAsync(SocketContext context)
        {
            return 0;
        }

        public override Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(Success());
        }
    }

    public class TestSocketCommandServerInitiatedFull : AbstractSocketCommand<SimpleModel, SocketParamModel>, IServerInitiatedCommand
    {
        public override string Name { get; set; } = "TestServerInitiatedFull";

        public override Task<ApiSocketResponse<SimpleModel>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(Success(new SimpleModel()));
        }
    }
}
