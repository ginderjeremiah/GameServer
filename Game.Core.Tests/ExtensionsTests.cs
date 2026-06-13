using Game.Core;
using Xunit;

namespace Game.Core.Tests
{
    public class ExtensionsTests
    {
        private record Sample(int Id, string Name);

        [Fact]
        public void ToBase64_NonNull_EncodesToStringRepresentation()
        {
            // "abc" UTF-8 -> base64
            Assert.Equal("YWJj", "abc".ToBase64());
        }

        [Fact]
        public void ToBase64_NullReceiver_EncodesEmptyString()
        {
            string? value = null;

            // The null-coalescing path falls back to "" which encodes to an empty base64 string.
            Assert.Equal("", value.ToBase64());
        }

        [Fact]
        public void Deserialize_HttpResponseWithContent_RoundTripsObject()
        {
            var original = new Sample(7, "thing");
            using var msg = new HttpResponseMessage
            {
                Content = new StringContent(original.Serialize()),
            };

            var result = msg.Deserialize<Sample>();

            Assert.Equal(original, result);
        }

        [Fact]
        public void Deserialize_HttpResponseWithEmptyStream_ReturnsDefault()
        {
            using var msg = new HttpResponseMessage
            {
                Content = new StringContent(""),
            };

            Assert.Null(msg.Deserialize<Sample>());
        }

        [Fact]
        public void Deserialize_NonNullString_RoundTripsObject()
        {
            var original = new Sample(3, "json");

            var result = original.Serialize().Deserialize<Sample>();

            Assert.Equal(original, result);
        }

        [Fact]
        public void Deserialize_NullString_ReturnsDefault()
        {
            string? json = null;

            Assert.Null(json.Deserialize<Sample>());
        }

        [Fact]
        public void WhereNotNull_ReferenceTypes_KeepsOnlyNonNull()
        {
            var source = new string?[] { "a", null, "b", null, "c" };

            Assert.Equal(new[] { "a", "b", "c" }, source.WhereNotNull());
        }

        [Fact]
        public void WhereNotNull_NullableValueTypes_UnwrapsNonNull()
        {
            var source = new int?[] { 1, null, 2, null, 3 };

            Assert.Equal(new[] { 1, 2, 3 }, source.WhereNotNull());
        }

        [Fact]
        public void SelectNotNull_ReferenceTypes_DiscardsNullResults()
        {
            var source = new[] { "keep", "", "also", "" };

            // Empty strings map to null and are filtered out.
            var result = source.SelectNotNull(s => s.Length == 0 ? null : s);

            Assert.Equal(new[] { "keep", "also" }, result);
        }

        [Fact]
        public void SelectNotNull_NullableValueTypes_DiscardsNullResults()
        {
            var source = new[] { 1, 2, 3, 4 };

            // Odd values map to null and are filtered out.
            var result = source.SelectNotNull(n => n % 2 == 0 ? (int?)n : null);

            Assert.Equal(new[] { 2, 4 }, result);
        }

        [Theory]
        [InlineData("HelloWorld", "helloWorld")]
        [InlineData("A", "a")]
        [InlineData("alreadyLower", "alreadyLower")]
        public void Decapitalize_LowercasesFirstCharacter(string input, string expected)
        {
            Assert.Equal(expected, input.Decapitalize());
        }

        [Fact]
        public void Decapitalize_EmptyString_ReturnsEmpty()
        {
            Assert.Equal("", "".Decapitalize());
        }

        [Theory]
        [InlineData("helloWorld", "HelloWorld")]
        [InlineData("a", "A")]
        [InlineData("AlreadyUpper", "AlreadyUpper")]
        public void Capitalize_UppercasesFirstCharacter(string input, string expected)
        {
            Assert.Equal(expected, input.Capitalize());
        }

        [Fact]
        public void Capitalize_EmptyString_ReturnsEmpty()
        {
            Assert.Equal("", "".Capitalize());
        }

        [Theory]
        [InlineData("HelloWorld", "Hello World")]
        [InlineData("oneTwoThree", "one Two Three")]
        [InlineData("NoBreaks", "No Breaks")]
        [InlineData("ALLCAPS", "ALLCAPS")]
        [InlineData("", "")]
        public void SpaceWords_InsertsSpaceBetweenLowerThenUpper(string input, string expected)
        {
            Assert.Equal(expected, input.SpaceWords());
        }

        [Theory]
        [InlineData("HelloWorld", "hello-world")]
        [InlineData("oneTwoThree", "one-two-three")]
        [InlineData("NoBreaks", "no-breaks")]
        [InlineData("nobreak", "nobreak")]
        [InlineData("", "")]
        public void SnakeCase_InsertsHyphenBetweenLowerThenUpperAndLowercases(string input, string expected)
        {
            Assert.Equal(expected, input.SnakeCase());
        }
    }
}
