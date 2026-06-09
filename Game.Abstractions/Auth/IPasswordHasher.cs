namespace Game.Abstractions.Auth
{
    /// <summary>
    /// Hashes and verifies account passwords. Abstracted here so the application layer can orchestrate
    /// credential checks without depending on the concrete key-derivation implementation. The current
    /// implementation derives keys with PBKDF2; the abstraction also supports transparently upgrading
    /// credentials that were stored under an older (or weaker-parameterised) scheme.
    /// </summary>
    public interface IPasswordHasher
    {
        /// <summary>
        /// Derives a self-describing hash of <paramref name="password"/> using the per-user
        /// <paramref name="salt"/> and the hasher's currently-configured parameters.
        /// </summary>
        string Hash(string password, Guid salt);

        /// <summary>
        /// Verifies <paramref name="password"/> against the <paramref name="storedHash"/> created with the
        /// given <paramref name="salt"/>. The comparison is constant-time. Returns
        /// <see cref="PasswordVerificationResult.SuccessRehashNeeded"/> when the credential is valid but
        /// was stored under a legacy scheme or with parameters that no longer match the current
        /// configuration, signalling the caller to re-hash and persist it.
        /// </summary>
        PasswordVerificationResult Verify(string password, Guid salt, string storedHash);
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
        /// The password matched, but the stored hash should be re-derived with the current scheme/parameters
        /// (e.g. a legacy hash being migrated on login).
        /// </summary>
        SuccessRehashNeeded,
    }
}
