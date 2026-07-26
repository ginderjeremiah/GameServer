using Game.Core;
using Game.Core.TestInfrastructure.Helpers;
using Xunit;

namespace Game.Core.Tests
{
    public class ExtensionsTests
    {
        private record Sample(int Id, string Name);

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
        [InlineData("iron", "Iron")]
        [InlineData("intelligence", "Intelligence")]
        public void Capitalize_TurkishCulture_UppercasesInvariantly(string input, string expected)
        {
            // tr-TR's culture-sensitive ToUpper maps 'i' to the dotted 'İ'. Callers feed the result into
            // EF seed values, so a locale-dependent name would be baked into the database rather than
            // merely mis-rendered.
            using var culture = new CultureScope("tr-TR");

            Assert.Equal(expected, input.Capitalize());
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

        [Fact]
        public void SpaceWords_TurkishCulture_BreaksWordsInvariantly()
        {
            // The word-break regex matches ordinal ranges and is not case-insensitive, so it is already
            // locale-independent; pinning it here keeps that true if the pattern is ever revisited.
            using var culture = new CultureScope("tr-TR");

            Assert.Equal("main Hand", "mainHand".SpaceWords());
        }
    }
}
