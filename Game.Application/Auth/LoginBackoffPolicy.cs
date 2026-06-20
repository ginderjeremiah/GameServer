using Game.Abstractions.DataAccess;
using Microsoft.Extensions.Options;

namespace Game.Application.Auth
{
    /// <summary>
    /// Computes the per-account login backoff: the first <see cref="LoginBackoffOptions.FailureThreshold"/>
    /// consecutive failures are free, after which each further failure delays the next permitted attempt by
    /// an exponentially growing window (doubling from the base delay up to a low cap). This is always a
    /// slowdown, never a hard lockout — a legitimate user is delayed by at most the cap — so an attacker who
    /// knows a username cannot lock the owner out, only briefly slow them. The logic is pure (the current
    /// instant is supplied by the caller), so it is unit-tested in isolation; the surrounding
    /// read-modify-write against Redis lives in <see cref="LoginBackoffGuard"/>.
    /// </summary>
    public class LoginBackoffPolicy(IOptions<LoginBackoffOptions> options)
    {
        private readonly LoginBackoffOptions _options = options.Value;

        /// <summary>
        /// Returns the remaining wait when the account is currently within an active backoff window (the
        /// caller should reject the attempt before touching credentials), or <see langword="null"/> when an
        /// attempt is permitted now.
        /// </summary>
        public TimeSpan? GetActiveBackoff(LoginBackoffState? state, DateTimeOffset now)
        {
            if (state is not null && state.LockedUntil > now)
            {
                return state.LockedUntil - now;
            }

            return null;
        }

        /// <summary>
        /// Computes the next state (and its retention TTL) after a failed attempt that passed the backoff
        /// gate, incrementing the consecutive-failure count and extending the lock per the exponential curve.
        /// </summary>
        public (LoginBackoffState State, TimeSpan Retention) RegisterFailure(LoginBackoffState? current, DateTimeOffset now)
        {
            var failureCount = (current?.FailureCount ?? 0) + 1;
            var delay = ComputeDelay(failureCount);
            // Keep the entry alive at least as long as the lock it represents, so an in-effect lock is never
            // dropped by a shorter window; otherwise the streak's sliding window governs retention.
            var retention = TimeSpan.FromSeconds(Math.Max(_options.FailureWindowSeconds, delay.TotalSeconds));
            return (new LoginBackoffState(failureCount, now + delay), retention);
        }

        private TimeSpan ComputeDelay(int failureCount)
        {
            var overage = failureCount - _options.FailureThreshold;
            if (overage <= 0)
            {
                return TimeSpan.Zero;
            }

            // Double from the base delay, then clamp to the cap before converting — computing in double and
            // clamping first means a long streak saturates at the cap instead of overflowing.
            var seconds = _options.BaseDelaySeconds * Math.Pow(2, overage - 1);
            return TimeSpan.FromSeconds(Math.Min(seconds, _options.MaxDelaySeconds));
        }
    }
}
