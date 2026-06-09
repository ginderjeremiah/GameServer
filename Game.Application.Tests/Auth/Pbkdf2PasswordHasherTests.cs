using System.Text;
using Game.Abstractions.Auth;
using Game.Application.Auth;
using Game.Core;
using Microsoft.Extensions.Options;
using Xunit;

namespace Game.Application.Tests.Auth
{
    /// <summary>
    /// Pure unit tests for the PBKDF2 password hasher: roundtrip verification, the self-describing
    /// format, work-factor and legacy-scheme rehash signalling, and graceful handling of malformed
    /// stored hashes. A low iteration count keeps the tests fast.
    /// </summary>
    public class Pbkdf2PasswordHasherTests
    {
        private const string Pepper = "unit-test-pepper";
        private const string Password = "correct horse battery staple";
        private static readonly Guid Salt = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static Pbkdf2PasswordHasher Create(int iterations = 1000, string pepper = Pepper)
        {
            return new Pbkdf2PasswordHasher(Options.Create(new PasswordHashingOptions
            {
                Pepper = pepper,
                Iterations = iterations,
            }));
        }

        [Fact]
        public void Hash_ProducesSelfDescribingFormat()
        {
            var hasher = Create(iterations: 2048);

            var hash = hasher.Hash(Password, Salt);

            Assert.StartsWith("$pbkdf2-sha256$2048$", hash);
        }

        [Fact]
        public void Verify_CorrectPassword_ReturnsSuccess()
        {
            var hasher = Create();
            var hash = hasher.Hash(Password, Salt);

            Assert.Equal(PasswordVerificationResult.Success, hasher.Verify(Password, Salt, hash));
        }

        [Fact]
        public void Verify_WrongPassword_ReturnsFailed()
        {
            var hasher = Create();
            var hash = hasher.Hash(Password, Salt);

            Assert.Equal(PasswordVerificationResult.Failed, hasher.Verify("wrong password", Salt, hash));
        }

        [Fact]
        public void Verify_WrongSalt_ReturnsFailed()
        {
            var hasher = Create();
            var hash = hasher.Hash(Password, Salt);

            var otherSalt = Guid.Parse("22222222-2222-2222-2222-222222222222");
            Assert.Equal(PasswordVerificationResult.Failed, hasher.Verify(Password, otherSalt, hash));
        }

        [Fact]
        public void Verify_HashStoredWithOutdatedIterations_ReturnsRehashNeeded()
        {
            var oldHash = Create(iterations: 1000).Hash(Password, Salt);
            var currentHasher = Create(iterations: 2000);

            Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, currentHasher.Verify(Password, Salt, oldHash));
        }

        [Fact]
        public void Verify_LegacyHash_CorrectPassword_ReturnsRehashNeeded()
        {
            var legacyHash = LegacyPasswordHash.Hash(Password, Salt.ToString(), Encoding.UTF8.GetBytes(Pepper));
            var hasher = Create();

            Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, hasher.Verify(Password, Salt, legacyHash));
        }

        [Fact]
        public void Verify_LegacyHash_WrongPassword_ReturnsFailed()
        {
            var legacyHash = LegacyPasswordHash.Hash(Password, Salt.ToString(), Encoding.UTF8.GetBytes(Pepper));
            var hasher = Create();

            Assert.Equal(PasswordVerificationResult.Failed, hasher.Verify("wrong password", Salt, legacyHash));
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-real-hash")]
        [InlineData("$pbkdf2-sha256$")]
        [InlineData("$pbkdf2-sha256$notanumber$abc")]
        [InlineData("$pbkdf2-sha256$1000$not!valid!base64")]
        public void Verify_MalformedStoredHash_ReturnsFailedWithoutThrowing(string storedHash)
        {
            var hasher = Create();

            Assert.Equal(PasswordVerificationResult.Failed, hasher.Verify(Password, Salt, storedHash));
        }
    }
}
