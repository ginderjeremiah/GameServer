using Game.Api;
using System.Net.WebSockets;
using Xunit;

namespace Game.Api.Tests.Unit
{
    public class SocketCloseReasonTests
    {
        [Fact]
        public void GetDescription_ServerShuttingDown_DescribesServerShutdown()
        {
            var description = ESocketCloseReason.ServerShuttingDown.GetDescription();

            Assert.Contains("shutting down", description, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetDescription_MessageTooBig_DescribesOversizedMessage()
        {
            var description = ESocketCloseReason.MessageTooBig.GetDescription();

            Assert.Contains("maximum allowed size", description, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetDescription_MalformedFrame_DescribesUnparseableFrame()
        {
            var description = ESocketCloseReason.MalformedFrame.GetDescription();

            Assert.Contains("could not be parsed", description, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(ESocketCloseReason.Finished)]
        [InlineData(ESocketCloseReason.Inactivity)]
        [InlineData(ESocketCloseReason.SocketReplaced)]
        [InlineData(ESocketCloseReason.MessageTooBig)]
        [InlineData(ESocketCloseReason.ServerShuttingDown)]
        [InlineData(ESocketCloseReason.MalformedFrame)]
        public void GetDescription_AnyReason_ReturnsNonEmptyDescription(ESocketCloseReason reason)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason.GetDescription()));
        }

        [Fact]
        public void GetDescription_EveryDefinedReason_HasADistinctNonDefaultDescription()
        {
            var reasons = Enum.GetValues<ESocketCloseReason>();
            var descriptions = reasons.Select(r => r.GetDescription()).ToList();

            // Every defined reason must map to its own description; none should silently
            // inherit another reason's text (the bug that motivated this test).
            Assert.Equal(reasons.Length, descriptions.Distinct().Count());
        }

        [Fact]
        public void GetDescription_UndefinedReason_Throws()
        {
            var undefined = (ESocketCloseReason)int.MaxValue;

            Assert.Throws<ArgumentOutOfRangeException>(() => undefined.GetDescription());
        }

        [Theory]
        [InlineData(ESocketCloseReason.Finished)]
        [InlineData(ESocketCloseReason.SocketReplaced)]
        [InlineData(ESocketCloseReason.ServerShuttingDown)]
        public void GetCloseStatus_GracefulReason_IsNormalClosure(ESocketCloseReason reason)
        {
            // A finished connection, an intentional takeover, and a planned shutdown are all clean closures —
            // they must not be reported as errors by status code.
            Assert.Equal(WebSocketCloseStatus.NormalClosure, reason.GetCloseStatus());
        }

        [Fact]
        public void GetCloseStatus_MessageTooBig_IsMessageTooBig()
        {
            Assert.Equal(WebSocketCloseStatus.MessageTooBig, ESocketCloseReason.MessageTooBig.GetCloseStatus());
        }

        [Fact]
        public void GetCloseStatus_Inactivity_IsPolicyViolation()
        {
            Assert.Equal(WebSocketCloseStatus.PolicyViolation, ESocketCloseReason.Inactivity.GetCloseStatus());
        }

        [Fact]
        public void GetCloseStatus_MalformedFrame_IsInvalidPayloadData()
        {
            Assert.Equal(WebSocketCloseStatus.InvalidPayloadData, ESocketCloseReason.MalformedFrame.GetCloseStatus());
        }

        [Theory]
        [InlineData(ESocketCloseReason.Inactivity)]
        [InlineData(ESocketCloseReason.MessageTooBig)]
        [InlineData(ESocketCloseReason.MalformedFrame)]
        public void GetCloseStatus_NonGracefulReason_IsNotNormalClosure(ESocketCloseReason reason)
        {
            // The motivating bug: error/abnormal closures previously reported NormalClosure, so a client
            // inspecting the status code couldn't tell them apart from a clean finish.
            Assert.NotEqual(WebSocketCloseStatus.NormalClosure, reason.GetCloseStatus());
        }

        [Theory]
        [InlineData(ESocketCloseReason.Finished)]
        [InlineData(ESocketCloseReason.Inactivity)]
        [InlineData(ESocketCloseReason.SocketReplaced)]
        [InlineData(ESocketCloseReason.MessageTooBig)]
        [InlineData(ESocketCloseReason.ServerShuttingDown)]
        [InlineData(ESocketCloseReason.MalformedFrame)]
        public void GetCloseStatus_AnyDefinedReason_ReturnsAStatus(ESocketCloseReason reason)
        {
            // Every defined reason must map to a status; an unhandled one would throw rather than fall through.
            Assert.True(Enum.IsDefined(reason.GetCloseStatus()));
        }

        [Fact]
        public void GetCloseStatus_UndefinedReason_Throws()
        {
            var undefined = (ESocketCloseReason)int.MaxValue;

            Assert.Throws<ArgumentOutOfRangeException>(() => undefined.GetCloseStatus());
        }
    }
}
