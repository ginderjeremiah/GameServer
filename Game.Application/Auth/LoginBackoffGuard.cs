using Game.Abstractions.DataAccess;

namespace Game.Application.Auth
{
    /// <summary>
    /// Coordinates the per-account login backoff: reads/writes the consecutive-failure state through
    /// <see cref="ILoginBackoffStore"/>, applies <see cref="LoginBackoffPolicy"/>, and supplies the current
    /// instant via the injected <see cref="TimeProvider"/>. The login orchestration uses it to gate, record,
    /// and reset attempts. Being coupled to the store (an out-of-process dependency) it holds no logic worth
    /// unit-testing itself — the backoff arithmetic lives in the pure <see cref="LoginBackoffPolicy"/>, and
    /// the wiring is covered by integration tests.
    /// </summary>
    public class LoginBackoffGuard(
        ILoginBackoffStore store,
        LoginBackoffPolicy policy,
        TimeProvider timeProvider)
    {
        private readonly ILoginBackoffStore _store = store;
        private readonly LoginBackoffPolicy _policy = policy;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <summary>
        /// Returns the remaining wait when <paramref name="username"/> is currently backed off (the caller
        /// should reject before verifying credentials), or <see langword="null"/> when an attempt is allowed.
        /// </summary>
        public async Task<TimeSpan?> GetActiveBackoff(string username, CancellationToken cancellationToken = default)
        {
            var state = await _store.Get(username, cancellationToken);
            return _policy.GetActiveBackoff(state, _timeProvider.GetUtcNow());
        }

        /// <summary>
        /// Records a failed login attempt, incrementing the account's consecutive-failure count and extending
        /// its backoff window per the policy. Only ever called for attempts that passed
        /// <see cref="GetActiveBackoff"/>, so spamming during an active window neither extends it nor spends
        /// server credential work — the gate rejects those attempts before they reach here.
        /// </summary>
        public async Task RegisterFailure(string username, CancellationToken cancellationToken = default)
        {
            var current = await _store.Get(username, cancellationToken);
            var (next, retention) = _policy.RegisterFailure(current, _timeProvider.GetUtcNow());
            await _store.Set(username, next, retention, cancellationToken);
        }

        /// <summary>Clears the account's failure streak after its credentials verify (a correct password is
        /// not a brute-force guess), so a legitimate login always starts the next streak from zero.</summary>
        public Task Reset(string username, CancellationToken cancellationToken = default)
        {
            return _store.Clear(username, cancellationToken);
        }
    }
}
