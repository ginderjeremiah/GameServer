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

        [Fact]
        public void GetDescription_MessageTooBig_DescribesOversizedMessage()
        {
            var description = ESocketCloseReason.MessageTooBig.GetDescription();

            Assert.Contains("maximum allowed size", description, StringComparison.OrdinalIgnoreCase);
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
    }
}
