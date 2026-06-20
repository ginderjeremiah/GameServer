using Game.Abstractions.DataAccess;
using Game.Application.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace Game.Application.Tests.Auth
{
    /// <summary>
    /// Pure unit tests for the per-account login backoff arithmetic: the free-attempt threshold, the
    /// exponential doubling and its low cap, the retention TTL, and the active-window check. The current
    /// instant is supplied to every call, so the time-based logic is exercised deterministically with no
    /// clock or Redis.
    /// </summary>
    public class LoginBackoffPolicyTests
    {
        private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        private static LoginBackoffPolicy Create(
            int failureThreshold = 3,
            int baseDelaySeconds = 2,
            int maxDelaySeconds = 1000,
            int failureWindowSeconds = 900)
        {
            return new LoginBackoffPolicy(Options.Create(new LoginBackoffOptions
            {
                FailureThreshold = failureThreshold,
                BaseDelaySeconds = baseDelaySeconds,
                MaxDelaySeconds = maxDelaySeconds,
                FailureWindowSeconds = failureWindowSeconds,
            }));
        }

        [Fact]
        public void GetActiveBackoff_NoState_ReturnsNull()
        {
            Assert.Null(Create().GetActiveBackoff(null, Now));
        }

        [Fact]
        public void GetActiveBackoff_LockInFuture_ReturnsRemainingWait()
        {
            var policy = Create();
            var state = new LoginBackoffState(5, Now.AddSeconds(10));

            Assert.Equal(TimeSpan.FromSeconds(10), policy.GetActiveBackoff(state, Now));
            Assert.Equal(TimeSpan.FromSeconds(4), policy.GetActiveBackoff(state, Now.AddSeconds(6)));
        }

        [Fact]
        public void GetActiveBackoff_LockAtOrPastNow_ReturnsNull()
        {
            var policy = Create();
            var state = new LoginBackoffState(5, Now.AddSeconds(10));

            // The instant the lock elapses (and after) is no longer backed off.
            Assert.Null(policy.GetActiveBackoff(state, Now.AddSeconds(10)));
            Assert.Null(policy.GetActiveBackoff(state, Now.AddSeconds(11)));
        }

        [Fact]
        public void RegisterFailure_FromNoState_StartsCountAtOne()
        {
            var (state, _) = Create().RegisterFailure(null, Now);

            Assert.Equal(1, state.FailureCount);
        }

        [Fact]
        public void RegisterFailure_IncrementsPriorCount()
        {
            var (state, _) = Create().RegisterFailure(new LoginBackoffState(4, Now), Now);

            Assert.Equal(5, state.FailureCount);
        }

        [Fact]
        public void RegisterFailure_AtOrBelowThreshold_AppliesNoDelay()
        {
            var policy = Create(failureThreshold: 3, baseDelaySeconds: 2);

            // The threshold-th failure still carries no lock (LockedUntil == now), so it is not backed off.
            var (state, _) = policy.RegisterFailure(new LoginBackoffState(2, Now), Now);

            Assert.Equal(3, state.FailureCount);
            Assert.Equal(Now, state.LockedUntil);
            Assert.Null(policy.GetActiveBackoff(state, Now));
            Assert.Null(policy.GetActiveBackoff(state, Now.AddSeconds(1)));
        }

        [Fact]
        public void RegisterFailure_FirstFailurePastThreshold_DelaysByBase()
        {
            var policy = Create(failureThreshold: 3, baseDelaySeconds: 5, maxDelaySeconds: 1000);

            var (state, _) = policy.RegisterFailure(new LoginBackoffState(3, Now), Now);

            Assert.Equal(TimeSpan.FromSeconds(5), state.LockedUntil - Now);
        }

        [Fact]
        public void RegisterFailure_PastThreshold_DoublesEachFailure()
        {
            var policy = Create(failureThreshold: 3, baseDelaySeconds: 2, maxDelaySeconds: 1000);

            // Failure counts 4, 5, 6 → base * {2^0, 2^1, 2^2} = 2s, 4s, 8s.
            Assert.Equal(TimeSpan.FromSeconds(2), policy.RegisterFailure(new LoginBackoffState(3, Now), Now).State.LockedUntil - Now);
            Assert.Equal(TimeSpan.FromSeconds(4), policy.RegisterFailure(new LoginBackoffState(4, Now), Now).State.LockedUntil - Now);
            Assert.Equal(TimeSpan.FromSeconds(8), policy.RegisterFailure(new LoginBackoffState(5, Now), Now).State.LockedUntil - Now);
        }

        [Fact]
        public void RegisterFailure_DelayIsCappedAtMax()
        {
            var policy = Create(failureThreshold: 0, baseDelaySeconds: 10, maxDelaySeconds: 30);

            // 10, 20, then 40 would exceed the cap and is clamped; a very long streak stays at the cap rather
            // than overflowing.
            Assert.Equal(TimeSpan.FromSeconds(10), policy.RegisterFailure(null, Now).State.LockedUntil - Now);
            Assert.Equal(TimeSpan.FromSeconds(20), policy.RegisterFailure(new LoginBackoffState(1, Now), Now).State.LockedUntil - Now);
            Assert.Equal(TimeSpan.FromSeconds(30), policy.RegisterFailure(new LoginBackoffState(2, Now), Now).State.LockedUntil - Now);
            Assert.Equal(TimeSpan.FromSeconds(30), policy.RegisterFailure(new LoginBackoffState(50, Now), Now).State.LockedUntil - Now);
        }

        [Fact]
        public void RegisterFailure_RetentionIsTheFailureWindowWhenItDominates()
        {
            var policy = Create(baseDelaySeconds: 1, failureWindowSeconds: 900);

            var (_, retention) = policy.RegisterFailure(null, Now);

            Assert.Equal(TimeSpan.FromSeconds(900), retention);
        }

        [Fact]
        public void RegisterFailure_RetentionNeverShorterThanTheActiveLock()
        {
            // A window shorter than the delay must not let the entry (and its lock) expire early.
            var policy = Create(failureThreshold: 0, baseDelaySeconds: 50, maxDelaySeconds: 1000, failureWindowSeconds: 5);

            var (_, retention) = policy.RegisterFailure(null, Now);

            Assert.Equal(TimeSpan.FromSeconds(50), retention);
        }
    }
}
