namespace Game.Abstractions.Auth
{
    /// <summary>
    /// Hashes and verifies account passwords. Abstracted here so the application layer can orchestrate
    /// credential checks without depending on the concrete key-derivation implementation. The current
    /// implementation derives keys with PBKDF2; the abstraction also supports transparently upgrading
    /// credentials that were stored with parameters that no longer match the current configuration.
    /// </summary>
    public interface IPasswordHasher
    {
        /// <summary>
        /// Derives a self-describing hash of <paramref name="password"/> using a fresh random salt and the
        /// hasher's currently-configured parameters. The salt is embedded in the returned hash, so it is
        /// fully self-contained.
        /// </summary>
        string Hash(string password);

        /// <summary>
        /// Verifies <paramref name="password"/> against the self-contained <paramref name="storedHash"/>
        /// (which carries its own salt and parameters). The comparison is constant-time. Returns
        /// <see cref="PasswordVerificationResult.SuccessRehashNeeded"/> when the credential is valid but
        /// was stored with parameters that no longer match the current configuration, signalling the
        /// caller to re-hash and persist it.
        /// </summary>
        PasswordVerificationResult Verify(string password, string storedHash);

        /// <summary>
        /// Performs password-derivation work equivalent to a real <see cref="Verify"/> against a fixed
        /// internal dummy hash, discarding the result. Called on the unknown-username login branch so it
        /// spends the same time as a known-username verify, preventing username enumeration by response
        /// timing (the present-vs-absent-account branch a constant-time hash comparison alone cannot cover).
        /// </summary>
        void VerifyDummy(string password);
    }

    /// <summary>
    /// The outcome of <see cref="IPasswordHasher.Verify"/>.
    /// </summary>
    public enum PasswordVerificationResult
    {
        /// <summary>The password did not match the stored hash.</summary>
        Failed,

        /// <summary>The password matched and the stored hash is already up to date.</summary>
        Success,

        /// <summary>
        /// The password matched, but the stored hash should be re-derived with the current parameters
        /// (e.g. an outdated work factor being upgraded on login).
        /// </summary>
        SuccessRehashNeeded,
    }
}
