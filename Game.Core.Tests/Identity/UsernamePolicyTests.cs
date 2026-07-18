using Game.Core.Identity;
using Xunit;

namespace Game.Core.Tests.Identity
{
    public class UsernamePolicyTests
    {
        [Theory]
        [InlineData("admin")]
        [InlineData("A")] // minimum length
        [InlineData("Player_123")]
        [InlineData("12345678901234567890")] // exactly the 20-char max
        public void TryNormalize_ValidUsername_ReturnsTrueWithSameValue(string username)
        {
            var valid = UsernamePolicy.TryNormalize(username, out var normalized);

            Assert.True(valid);
            Assert.Equal(username, normalized);
        }

        [Theory]
        [InlineData("  admin  ", "admin")]
        [InlineData("\tadmin\n", "admin")]
        [InlineData("   A   ", "A")]
        public void TryNormalize_SurroundingWhitespace_IsTrimmed(string username, string expected)
        {
            var valid = UsernamePolicy.TryNormalize(username, out var normalized);

            Assert.True(valid);
            Assert.Equal(expected, normalized);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")] // whitespace-only trims to empty
        [InlineData("\t\n")]
        public void TryNormalize_BlankUsername_ReturnsFalse(string? username)
        {
            var valid = UsernamePolicy.TryNormalize(username, out var normalized);

            Assert.False(valid);
            Assert.Null(normalized);
        }

        [Theory]
        [InlineData("123456789012345678901")] // 21 chars — one over the max
        [InlineData("this-username-is-definitely-too-long")]
        public void TryNormalize_TooLong_ReturnsFalse(string username)
        {
            var valid = UsernamePolicy.TryNormalize(username, out var normalized);

            Assert.False(valid);
            Assert.Null(normalized);
        }

        [Theory]
        [InlineData("bad\tname")] // embedded tab
        [InlineData("bad\nname")] // embedded newline
        [InlineData("bad\0name")] // embedded null
        public void TryNormalize_ContainsControlCharacters_ReturnsFalse(string username)
        {
            var valid = UsernamePolicy.TryNormalize(username, out var normalized);

            Assert.False(valid);
            Assert.Null(normalized);
        }

        [Theory]
        [InlineData("bad\u200Bname")] // zero width space
        [InlineData("bad\u200Cname")] // zero width non-joiner
        [InlineData("bad\u200Dname")] // zero width joiner
        [InlineData("bad\uFEFFname")] // zero width no-break space / BOM
        public void TryNormalize_ContainsZeroWidthCharacters_ReturnsFalse(string username)
        {
            var valid = UsernamePolicy.TryNormalize(username, out var normalized);

            Assert.False(valid);
            Assert.Null(normalized);
        }
    }
}
