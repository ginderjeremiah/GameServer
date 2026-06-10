using Game.Abstractions.Auth;
using Game.Application.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace Game.Application.Tests.Auth
{
    /// <summary>
    /// Pure unit tests for the PBKDF2 password hasher: roundtrip verification, the self-describing
    /// salt-embedding format, per-hash salt uniqueness, work-factor rehash signalling, and graceful
    /// handling of malformed stored hashes. A low iteration count keeps the tests fast.
    /// </summary>
    public class Pbkdf2PasswordHasherTests
    {
        private const string Pepper = "unit-test-pepper";
        private const string Password = "correct horse battery staple";

        private static Pbkdf2PasswordHasher Create(int iterations = 1000, string pepper = Pepper)
        {
            return new Pbkdf2PasswordHasher(Options.Create(new PasswordHashingOptions
            {
                Pepper = pepper,
                Iterations = iterations,
            }));
        }

        [Fact]
        public void Hash_ProducesSelfDescribingFormatWithEmbeddedSalt()
        {
            var hasher = Create(iterations: 2048);

            var hash = hasher.Hash(Password);

            // $pbkdf2-sha256$<iterations>$<base64-salt>$<base64-key>
            Assert.StartsWith("$pbkdf2-sha256$2048$", hash);
            Assert.Equal(4, hash.Split('$', StringSplitOptions.RemoveEmptyEntries).Length);
        }

        [Fact]
        public void Hash_GeneratesUniqueSaltPerCall()
        {
            var hasher = Create();

            var first = hasher.Hash(Password);
            var second = hasher.Hash(Password);

            // A fresh random salt per hash means two hashes of the same password never collide,
            // yet both still verify.
            Assert.NotEqual(first, second);
            Assert.Equal(PasswordVerificationResult.Success, hasher.Verify(Password, first));
            Assert.Equal(PasswordVerificationResult.Success, hasher.Verify(Password, second));
        }

        [Fact]
        public void Verify_CorrectPassword_ReturnsSuccess()
        {
            var hasher = Create();
            var hash = hasher.Hash(Password);

            Assert.Equal(PasswordVerificationResult.Success, hasher.Verify(Password, hash));
        }

        [Fact]
        public void Verify_WrongPassword_ReturnsFailed()
        {
            var hasher = Create();
            var hash = hasher.Hash(Password);

            Assert.Equal(PasswordVerificationResult.Failed, hasher.Verify("wrong password", hash));
        }

        [Fact]
        public void Verify_DifferentPepper_ReturnsFailed()
        {
            var hash = Create(pepper: Pepper).Hash(Password);
            var otherPepperHasher = Create(pepper: "a-different-pepper");

            Assert.Equal(PasswordVerificationResult.Failed, otherPepperHasher.Verify(Password, hash));
        }

        [Fact]
        public void Verify_HashStoredWithOutdatedIterations_ReturnsRehashNeeded()
        {
            var oldHash = Create(iterations: 1000).Hash(Password);
            var currentHasher = Create(iterations: 2000);

            Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, currentHasher.Verify(Password, oldHash));
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-real-hash")]
        [InlineData("$pbkdf2-sha256$")]
        [InlineData("$pbkdf2-sha256$notanumber$c2FsdA==$abc")]
        [InlineData("$pbkdf2-sha256$1000$c2FsdA==")] // missing the key segment
        [InlineData("$pbkdf2-sha256$1000$not!valid!base64$abc")] // bad salt
        [InlineData("$pbkdf2-sha256$1000$c2FsdA==$not!valid!base64")] // bad key
        public void Verify_MalformedStoredHash_ReturnsFailedWithoutThrowing(string storedHash)
        {
            var hasher = Create();

            Assert.Equal(PasswordVerificationResult.Failed, hasher.Verify(Password, storedHash));
        }
    }
}
