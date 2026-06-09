using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Game.Abstractions.Auth;
using Game.Core;
using Microsoft.Extensions.Options;

namespace Game.Application.Auth
{
    /// <summary>
    /// Derives password hashes with PBKDF2 (HMAC-SHA256), a purpose-built, deliberately-slow key
    /// derivation function, replacing the bespoke iterated-SHA-512 construction. New hashes are stored in
    /// a self-describing format (<c>$pbkdf2-sha256$&lt;iterations&gt;$&lt;base64-key&gt;</c>) so the work
    /// factor is recorded alongside the hash and can be raised over time. An application-wide pepper is
    /// folded in via a standard HMAC pre-hash (defence-in-depth), supplied through
    /// <see cref="PasswordHashingOptions"/> rather than process-global state.
    /// </summary>
    /// <remarks>
    /// The per-user salt is the existing <see cref="Guid"/> credential salt (a 128-bit random value),
    /// reused as the PBKDF2 salt so no schema change is required. Credentials still stored under the
    /// legacy scheme are verified via <see cref="LegacyPasswordHash"/> and reported as
    /// <see cref="PasswordVerificationResult.SuccessRehashNeeded"/> so the caller can migrate them on login.
    /// </remarks>
    public class Pbkdf2PasswordHasher : IPasswordHasher
    {
        private const string FormatPrefix = "$pbkdf2-sha256$";
        private const int KeyLengthBytes = 32;
        private static readonly HashAlgorithmName DerivationAlgorithm = HashAlgorithmName.SHA256;

        private readonly int _iterations;
        private readonly byte[] _pepper;

        public Pbkdf2PasswordHasher(IOptions<PasswordHashingOptions> options)
        {
            var value = options.Value;
            _iterations = value.Iterations;
            _pepper = Encoding.UTF8.GetBytes(value.Pepper);
        }

        public string Hash(string password, Guid salt)
        {
            var derived = Derive(password, salt, _iterations);
            return string.Concat(
                FormatPrefix,
                _iterations.ToString(CultureInfo.InvariantCulture),
                "$",
                Convert.ToBase64String(derived));
        }

        public PasswordVerificationResult Verify(string password, Guid salt, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash))
            {
                return PasswordVerificationResult.Failed;
            }

            return storedHash.StartsWith(FormatPrefix, StringComparison.Ordinal)
                ? VerifyCurrent(password, salt, storedHash)
                : VerifyLegacy(password, salt, storedHash);
        }

        private PasswordVerificationResult VerifyCurrent(string password, Guid salt, string storedHash)
        {
            if (!TryParse(storedHash, out var iterations, out var expected))
            {
                return PasswordVerificationResult.Failed;
            }

            var computed = Derive(password, salt, iterations);
            if (!CryptographicOperations.FixedTimeEquals(computed, expected))
            {
                return PasswordVerificationResult.Failed;
            }

            // A valid hash stored with an outdated work factor should be re-derived at the current one.
            return iterations == _iterations
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.SuccessRehashNeeded;
        }

        private PasswordVerificationResult VerifyLegacy(string password, Guid salt, string storedHash)
        {
            var computed = Encoding.UTF8.GetBytes(LegacyPasswordHash.Hash(password, salt.ToString(), _pepper));
            var expected = Encoding.UTF8.GetBytes(storedHash);

            // A legacy match is always due for migration to the PBKDF2 scheme.
            return CryptographicOperations.FixedTimeEquals(computed, expected)
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Failed;
        }

        private byte[] Derive(string password, Guid salt, int iterations)
        {
            // Fold the application-wide pepper into the secret via a standard HMAC pre-hash so both the
            // per-user salt and the global secret contribute, without a bespoke mixing construction.
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] secret;
            if (_pepper.Length > 0)
            {
                secret = HMACSHA256.HashData(_pepper, passwordBytes);
            }
            else
            {
                secret = passwordBytes;
            }

            return Rfc2898DeriveBytes.Pbkdf2(secret, salt.ToByteArray(), iterations, DerivationAlgorithm, KeyLengthBytes);
        }

        private static bool TryParse(string storedHash, out int iterations, out byte[] hash)
        {
            iterations = 0;
            hash = [];

            var body = storedHash.AsSpan(FormatPrefix.Length);
            var separator = body.IndexOf('$');
            if (separator <= 0)
            {
                return false;
            }

            if (!int.TryParse(body[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out iterations) || iterations < 1)
            {
                return false;
            }

            try
            {
                hash = Convert.FromBase64String(body[(separator + 1)..].ToString());
            }
            catch (FormatException)
            {
                return false;
            }

            return hash.Length > 0;
        }
    }
}
