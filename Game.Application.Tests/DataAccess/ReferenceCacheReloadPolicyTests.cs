using Game.DataAccess;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Unit tests for <see cref="ReferenceCacheReloadPolicy"/>: the debounce-window and reconciliation-interval
    /// guards and default values this record adds on top of the base <see cref="RetryPolicy"/> (whose shared
    /// guards and backoff math are pinned by <see cref="PlayerUpdateRetryPolicyTests"/>).
    /// </summary>
    public class ReferenceCacheReloadPolicyTests
    {
        [Fact]
        public void Constructor_NegativeDebounceWindow_Throws()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => new ReferenceCacheReloadPolicy(TimeSpan.FromMilliseconds(-1), maxAttempts: 3, baseDelay: TimeSpan.Zero));
            Assert.Equal("debounceWindow", ex.ParamName);
        }

        [Fact]
        public void Constructor_ZeroDebounceWindow_IsAllowed()
        {
            var policy = new ReferenceCacheReloadPolicy(TimeSpan.Zero, maxAttempts: 1, baseDelay: TimeSpan.Zero);

            Assert.Equal(TimeSpan.Zero, policy.DebounceWindow);
        }

        [Fact]
        public void Constructor_ValidArguments_SetsProperties()
        {
            var policy = new ReferenceCacheReloadPolicy(TimeSpan.FromMilliseconds(100), maxAttempts: 4, baseDelay: TimeSpan.FromMilliseconds(250));

            Assert.Equal(TimeSpan.FromMilliseconds(100), policy.DebounceWindow);
            Assert.Equal(4, policy.MaxAttempts);
            Assert.Equal(TimeSpan.FromMilliseconds(250), policy.BaseDelay);
            Assert.Null(policy.ReconciliationInterval);
        }

        [Fact]
        public void Constructor_ReconciliationIntervalOmitted_DefaultsToDisabled()
        {
            var policy = new ReferenceCacheReloadPolicy(TimeSpan.Zero, maxAttempts: 1, baseDelay: TimeSpan.Zero);

            Assert.Null(policy.ReconciliationInterval);
        }

        [Fact]
        public void Constructor_PositiveReconciliationInterval_IsAllowed()
        {
            var policy = new ReferenceCacheReloadPolicy(TimeSpan.Zero, maxAttempts: 1, baseDelay: TimeSpan.Zero, reconciliationInterval: TimeSpan.FromMinutes(2));

            Assert.Equal(TimeSpan.FromMinutes(2), policy.ReconciliationInterval);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Constructor_NonPositiveReconciliationInterval_Throws(int seconds)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => new ReferenceCacheReloadPolicy(TimeSpan.Zero, maxAttempts: 1, baseDelay: TimeSpan.Zero, reconciliationInterval: TimeSpan.FromSeconds(seconds)));
            Assert.Equal("reconciliationInterval", ex.ParamName);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Constructor_MaxAttemptsBelowOne_Throws(int maxAttempts)
        {
            // The base guard must apply through this record's constructor too.
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => new ReferenceCacheReloadPolicy(TimeSpan.Zero, maxAttempts, TimeSpan.Zero));
            Assert.Equal("maxAttempts", ex.ParamName);
        }

        [Fact]
        public void Default_Has500msDebounceFiveAttemptsWith1sBackoffAndA5MinuteReconciliationInterval()
        {
            Assert.Equal(TimeSpan.FromMilliseconds(500), ReferenceCacheReloadPolicy.Default.DebounceWindow);
            Assert.Equal(5, ReferenceCacheReloadPolicy.Default.MaxAttempts);
            Assert.Equal(TimeSpan.FromSeconds(1), ReferenceCacheReloadPolicy.Default.BaseDelay);
            Assert.Equal(TimeSpan.FromMinutes(5), ReferenceCacheReloadPolicy.Default.ReconciliationInterval);
        }
    }
}
