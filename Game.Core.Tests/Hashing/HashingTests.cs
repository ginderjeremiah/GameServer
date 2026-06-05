using Game.Core;
using Xunit;

namespace Game.Core.Tests.Hashing
{
    public class HashingTests
    {
        private const string Salt = "a-test-salt";

        [Fact]
        public void VerifyHash_CorrectPassword_ReturnsTrue()
        {
            var hash = "correct horse battery staple".Hash(Salt);

            var result = "correct horse battery staple".VerifyHash(Salt, hash);

            Assert.True(result);
        }

        [Fact]
        public void VerifyHash_WrongPassword_ReturnsFalse()
        {
            var hash = "correct horse battery staple".Hash(Salt);

            var result = "wrong password".VerifyHash(Salt, hash);

            Assert.False(result);
        }

        [Fact]
        public void VerifyHash_WrongSalt_ReturnsFalse()
        {
            var hash = "password".Hash(Salt);

            var result = "password".VerifyHash("a-different-salt", hash);

            Assert.False(result);
        }

        [Fact]
        public void VerifyHash_EmptyExpectedHash_ReturnsFalse()
        {
            var result = "password".VerifyHash(Salt, string.Empty);

            Assert.False(result);
        }

        [Fact]
        public void VerifyHash_GarbageExpectedHash_ReturnsFalse()
        {
            var result = "password".VerifyHash(Salt, "not-a-real-hash");

            Assert.False(result);
        }
    }
}
