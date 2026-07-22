using Game.Core.Identity;
using Xunit;

namespace Game.Core.Tests.Identity
{
    public class PasswordPolicyTests
    {
        [Theory]
        [InlineData("password1")]
        [InlineData("pass1234")] // exactly the 8-char minimum
        [InlineData("Str0ngPassphrase!")]
        public void IsValid_ValidPassword_ReturnsTrue(string password)
        {
            Assert.True(PasswordPolicy.IsValid(password));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("short1")] // 6 chars — under the minimum
        [InlineData("short12")] // 7 chars — one under the minimum
        public void IsValid_TooShort_ReturnsFalse(string? password)
        {
            Assert.False(PasswordPolicy.IsValid(password));
        }

        [Theory]
        [InlineData("nodigitshere")]
        [InlineData("alllettersnonumeric")]
        public void IsValid_NoDigit_ReturnsFalse(string password)
        {
            Assert.False(PasswordPolicy.IsValid(password));
        }

        [Theory]
        [InlineData("12345678")]
        [InlineData("9876543210")]
        public void IsValid_NoLetter_ReturnsFalse(string password)
        {
            Assert.False(PasswordPolicy.IsValid(password));
        }

        [Fact]
        public void IsValid_OverMaxLength_ReturnsFalse()
        {
            var tooLong = "a1" + new string('a', PasswordPolicy.MaxLength);

            Assert.False(PasswordPolicy.IsValid(tooLong));
        }

        [Fact]
        public void IsValid_AtMaxLength_ReturnsTrue()
        {
            var atMax = "a1" + new string('a', PasswordPolicy.MaxLength - 2);

            Assert.Equal(PasswordPolicy.MaxLength, atMax.Length);
            Assert.True(PasswordPolicy.IsValid(atMax));
        }

        [Fact]
        public void IsValid_SurroundingWhitespace_IsNotTrimmedBeforeLengthCheck()
        {
            // Unlike UsernamePolicy/PlayerName, passwords are never normalized: "ab1" padded to 8 raw
            // characters is valid here (whitespace counts toward length), but would fail a MinLength
            // check if a trim were ever (wrongly) added ahead of it — since the trimmed "ab1" is only
            // 3 characters. Pins that the raw string, not a trimmed one, is what gets validated and hashed.
            const string padded = "  ab1   ";
            Assert.Equal(8, padded.Length);

            Assert.True(PasswordPolicy.IsValid(padded));
        }
    }
}
