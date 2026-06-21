using Game.Core.Players;
using Xunit;

namespace Game.Core.Tests.Players
{
    public class PlayerNameTests
    {
        [Theory]
        [InlineData("Hero")]
        [InlineData("A")] // minimum length
        [InlineData("Sir Lancelot")] // internal spaces allowed
        [InlineData("Player_123")]
        [InlineData("12345678901234567890")] // exactly the 20-char max
        public void TryNormalize_ValidName_ReturnsTrueWithSameValue(string name)
        {
            var valid = PlayerName.TryNormalize(name, out var normalized);

            Assert.True(valid);
            Assert.Equal(name, normalized);
        }

        [Theory]
        [InlineData("  Hero  ", "Hero")]
        [InlineData("\tHero\n", "Hero")]
        [InlineData("   A   ", "A")]
        public void TryNormalize_SurroundingWhitespace_IsTrimmed(string name, string expected)
        {
            var valid = PlayerName.TryNormalize(name, out var normalized);

            Assert.True(valid);
            Assert.Equal(expected, normalized);
        }

        [Fact]
        public void TryNormalize_NameTrimmedToExactlyMaxLength_IsValid()
        {
            // 20 visible chars wrapped in whitespace trims back to the 20-char limit.
            var padded = "   12345678901234567890   ";

            var valid = PlayerName.TryNormalize(padded, out var normalized);

            Assert.True(valid);
            Assert.Equal("12345678901234567890", normalized);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")] // whitespace-only trims to empty
        [InlineData("\t\n")]
        public void TryNormalize_BlankName_ReturnsFalse(string? name)
        {
            var valid = PlayerName.TryNormalize(name, out var normalized);

            Assert.False(valid);
            Assert.Null(normalized);
        }

        [Theory]
        [InlineData("123456789012345678901")] // 21 chars — one over the max
        [InlineData("This name is definitely far too long")]
        public void TryNormalize_TooLong_ReturnsFalse(string name)
        {
            var valid = PlayerName.TryNormalize(name, out var normalized);

            Assert.False(valid);
            Assert.Null(normalized);
        }

        [Theory]
        [InlineData("Bad\tName")] // embedded tab
        [InlineData("Bad\nName")] // embedded newline
        [InlineData("Bad\0Name")] // embedded null
        public void TryNormalize_ContainsControlCharacters_ReturnsFalse(string name)
        {
            var valid = PlayerName.TryNormalize(name, out var normalized);

            Assert.False(valid);
            Assert.Null(normalized);
        }
    }
}
