using System.Text;
using Game.Core;
using Xunit;

namespace Game.Core.Tests.Hashing
{
    /// <summary>
    /// Guards the retained legacy hash so it keeps producing the exact bytes stored for pre-PBKDF2
    /// credentials — the transparent login migration relies on it verifying those old hashes. It is a
    /// deterministic pure function of (password, salt, pepper).
    /// </summary>
    public class LegacyPasswordHashTests
    {
        private static readonly byte[] Pepper = Encoding.UTF8.GetBytes("a-test-pepper");
        private const string Salt = "a-test-salt";

        [Fact]
        public void Hash_IsDeterministic_ForSameInputs()
        {
            var first = LegacyPasswordHash.Hash("correct horse battery staple", Salt, Pepper);
            var second = LegacyPasswordHash.Hash("correct horse battery staple", Salt, Pepper);

            Assert.Equal(first, second);
        }

        [Fact]
        public void Hash_DiffersByPassword()
        {
            var hash = LegacyPasswordHash.Hash("password", Salt, Pepper);

            Assert.NotEqual(hash, LegacyPasswordHash.Hash("different", Salt, Pepper));
        }

        [Fact]
        public void Hash_DiffersBySalt()
        {
            var hash = LegacyPasswordHash.Hash("password", Salt, Pepper);

            Assert.NotEqual(hash, LegacyPasswordHash.Hash("password", "a-different-salt", Pepper));
        }

        [Fact]
        public void Hash_DiffersByPepper()
        {
            var hash = LegacyPasswordHash.Hash("password", Salt, Pepper);

            Assert.NotEqual(hash, LegacyPasswordHash.Hash("password", Salt, Encoding.UTF8.GetBytes("other-pepper")));
        }
    }
}
