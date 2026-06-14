using Game.Api;
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

        [Theory]
        [InlineData(ESocketCloseReason.Finished)]
        [InlineData(ESocketCloseReason.Inactivity)]
        [InlineData(ESocketCloseReason.SocketReplaced)]
        [InlineData(ESocketCloseReason.MessageTooBig)]
        [InlineData(ESocketCloseReason.ServerShuttingDown)]
        public void GetDescription_AnyReason_ReturnsNonEmptyDescription(ESocketCloseReason reason)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason.GetDescription()));
        }
    }
}
