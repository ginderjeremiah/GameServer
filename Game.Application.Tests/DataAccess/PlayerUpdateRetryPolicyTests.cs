using Game.DataAccess;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Unit tests for <see cref="PlayerUpdateRetryPolicy"/>: the constructor argument guards, the default
    /// policy values, and the exponential-backoff math in <see cref="PlayerUpdateRetryPolicy.DelayAfterAttempt"/>.
    /// </summary>
    public class PlayerUpdateRetryPolicyTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Constructor_MaxAttemptsBelowOne_Throws(int maxAttempts)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerUpdateRetryPolicy(maxAttempts, TimeSpan.FromMilliseconds(200)));
            Assert.Equal("maxAttempts", ex.ParamName);
        }

        [Fact]
        public void Constructor_NegativeBaseDelay_Throws()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerUpdateRetryPolicy(3, TimeSpan.FromMilliseconds(-1)));
            Assert.Equal("baseDelay", ex.ParamName);
        }

        [Fact]
        public void Constructor_ValidArguments_SetsProperties()
        {
            var policy = new PlayerUpdateRetryPolicy(5, TimeSpan.FromMilliseconds(250));

            Assert.Equal(5, policy.MaxAttempts);
            Assert.Equal(TimeSpan.FromMilliseconds(250), policy.BaseDelay);
        }

        [Fact]
        public void Constructor_ZeroBaseDelay_IsAllowed()
        {
            var policy = new PlayerUpdateRetryPolicy(1, TimeSpan.Zero);

            Assert.Equal(TimeSpan.Zero, policy.BaseDelay);
        }

        [Fact]
        public void Default_HasThreeAttemptsAnd200msBackoff()
        {
            Assert.Equal(3, PlayerUpdateRetryPolicy.Default.MaxAttempts);
            Assert.Equal(TimeSpan.FromMilliseconds(200), PlayerUpdateRetryPolicy.Default.BaseDelay);
        }

        [Theory]
        [InlineData(-1, 0)]   // out-of-range attempt -> no delay
        [InlineData(0, 0)]    // out-of-range attempt -> no delay
        [InlineData(1, 200)]  // 2^0 * base
        [InlineData(2, 400)]  // 2^1 * base
        [InlineData(3, 800)]  // 2^2 * base
        [InlineData(4, 1600)] // 2^3 * base
        public void DelayAfterAttempt_DoublesPerAttempt(int failedAttempt, int expectedMs)
        {
            var policy = new PlayerUpdateRetryPolicy(10, TimeSpan.FromMilliseconds(200));

            Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), policy.DelayAfterAttempt(failedAttempt));
        }

        [Fact]
        public void DelayAfterAttempt_ZeroBaseDelay_AlwaysZero()
        {
            var policy = new PlayerUpdateRetryPolicy(10, TimeSpan.Zero);

            Assert.Equal(TimeSpan.Zero, policy.DelayAfterAttempt(1));
            Assert.Equal(TimeSpan.Zero, policy.DelayAfterAttempt(5));
        }
    }
}
