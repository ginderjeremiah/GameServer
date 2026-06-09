namespace Game.Application.Auth
{
    /// <summary>
    /// Configuration for <see cref="Pbkdf2PasswordHasher"/>. The pepper is an application-wide secret
    /// supplied via configuration (it must never live in the database alongside the per-user salt); the
    /// iteration count is the tunable PBKDF2 work factor, raised over time as hardware improves.
    /// </summary>
    public class PasswordHashingOptions
    {
        /// <summary>
        /// Application-wide secret mixed into every hash (defence-in-depth: a stolen database alone cannot
        /// be cracked without it). Required — there is no default.
        /// </summary>
        public string Pepper { get; set; } = string.Empty;

        /// <summary>
        /// PBKDF2 iteration count. Defaults to the OWASP-recommended floor for PBKDF2-HMAC-SHA256.
        /// </summary>
        public int Iterations { get; set; } = 600_000;
    }
}
