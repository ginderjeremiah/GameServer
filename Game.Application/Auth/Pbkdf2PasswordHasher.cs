using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Game.Abstractions.Auth;
using Microsoft.Extensions.Options;

namespace Game.Application.Auth
{
    /// <summary>
    /// Derives password hashes with PBKDF2 (HMAC-SHA256), a purpose-built, deliberately-slow key
    /// derivation function. Hashes are stored in a self-describing format
    /// (<c>$pbkdf2-sha256$&lt;iterations&gt;$&lt;base64-salt&gt;$&lt;base64-key&gt;</c>) so both the work
    /// factor and the per-hash salt live alongside the key — the stored hash is fully self-contained and
    /// the work factor can be raised over time. An application-wide pepper is folded in via a standard
    /// HMAC pre-hash (defence-in-depth), supplied through <see cref="PasswordHashingOptions"/> rather than
    /// process-global state.
    /// </summary>
    /// <remarks>
    /// A fresh 128-bit random salt is generated for every hash and embedded in the output, so no separate
    /// salt storage is required. A credential stored with an outdated work factor verifies and is reported
    /// as <see cref="PasswordVerificationResult.SuccessRehashNeeded"/> so the caller can upgrade it on login.
    /// </remarks>
    public class Pbkdf2PasswordHasher : IPasswordHasher
    {
        private const string FormatPrefix = "$pbkdf2-sha256$";
        private const int KeyLengthBytes = 32;
        private const int SaltLengthBytes = 16;
        // A fixed plaintext whose hash an unknown-user login verifies against; its content is irrelevant
        // since the comparison is never expected to match — only the derivation cost matters.
        private const string DummyPassword = "unknown-user-timing-mitigation";
        private static readonly HashAlgorithmName DerivationAlgorithm = HashAlgorithmName.SHA256;

        private readonly int _iterations;
        private readonly byte[] _pepper;
        // A dummy hash at the current work factor, derived once on first use. Verifying a supplied password
        // against it costs the same PBKDF2 work as a real account, masking the unknown-user branch.
        private readonly Lazy<string> _dummyHash;

        public Pbkdf2PasswordHasher(IOptions<PasswordHashingOptions> options)
        {
            var value = options.Value;
            _iterations = value.Iterations;
            _pepper = Encoding.UTF8.GetBytes(value.Pepper);
            _dummyHash = new Lazy<string>(() => Hash(DummyPassword));
        }

        public string Hash(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltLengthBytes);
            var derived = Derive(password, salt, _iterations);
            return string.Concat(
                FormatPrefix,
                _iterations.ToString(CultureInfo.InvariantCulture),
                "$",
                Convert.ToBase64String(salt),
                "$",
                Convert.ToBase64String(derived));
        }

        public PasswordVerificationResult Verify(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash) || !TryParse(storedHash, out var iterations, out var salt, out var expected))
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

        public void VerifyDummy(string password)
        {
            // Run a full verify (derivation + constant-time compare) against the dummy hash and discard the
            // outcome — the call exists only to spend the same work a real verify would on a known username.
            Verify(password, _dummyHash.Value);
        }

        private byte[] Derive(string password, byte[] salt, int iterations)
        {
            // Fold the application-wide pepper into the secret via a standard HMAC pre-hash so both the
            // per-hash salt and the global secret contribute, without a bespoke mixing construction.
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

            return Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, DerivationAlgorithm, KeyLengthBytes);
        }

        private static bool TryParse(string storedHash, out int iterations, out byte[] salt, out byte[] hash)
        {
            iterations = 0;
            salt = [];
            hash = [];

            if (!storedHash.StartsWith(FormatPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var body = storedHash.AsSpan(FormatPrefix.Length);
            var iterationsEnd = body.IndexOf('$');
            if (iterationsEnd <= 0)
            {
                return false;
            }

            if (!int.TryParse(body[..iterationsEnd], NumberStyles.None, CultureInfo.InvariantCulture, out iterations) || iterations < 1)
            {
                return false;
            }

            var rest = body[(iterationsEnd + 1)..];
            var saltEnd = rest.IndexOf('$');
            if (saltEnd <= 0)
            {
                return false;
            }

            if (!TryDecodeBase64(rest[..saltEnd], out salt) || salt.Length == 0)
            {
                return false;
            }

            return TryDecodeBase64(rest[(saltEnd + 1)..], out hash) && hash.Length > 0;
        }

        private static bool TryDecodeBase64(ReadOnlySpan<char> value, out byte[] decoded)
        {
            try
            {
                decoded = Convert.FromBase64String(value.ToString());
                return true;
            }
            catch (FormatException)
            {
                decoded = [];
                return false;
            }
        }
    }
}
