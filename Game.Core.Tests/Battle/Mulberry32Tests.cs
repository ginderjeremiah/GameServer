using Game.Core.Battle;
using Xunit;

namespace Game.Core.Tests.Battle
{
    /// <summary>
    /// Behavioural unit tests for the backend <see cref="Mulberry32"/> RNG primitive.
    /// The cross-implementation contract (the exact output sequence the frontend port
    /// must also produce) lives in <see cref="Mulberry32ParityTests"/>; this suite only
    /// covers the language-agnostic properties (determinism, range, seed sensitivity).
    /// </summary>
    public class Mulberry32Tests
    {
        [Fact]
        public void SameSeed_ProducesIdenticalSequence()
        {
            var a = new Mulberry32(0x1234_5678);
            var b = new Mulberry32(0x1234_5678);

            for (var i = 0; i < 100; i++)
            {
                Assert.Equal(a.Next(), b.Next());
            }
        }

        [Fact]
        public void DifferentSeeds_DivergeImmediately()
        {
            var a = new Mulberry32(1);
            var b = new Mulberry32(2);

            Assert.NotEqual(a.Next(), b.Next());
        }

        [Theory]
        [InlineData(0u)]
        [InlineData(1u)]
        [InlineData(123456789u)]
        [InlineData(0xDEADBEEFu)]
        [InlineData(uint.MaxValue)]
        public void Next_AlwaysReturnsValueInUnitInterval(uint seed)
        {
            var rng = new Mulberry32(seed);

            for (var i = 0; i < 1000; i++)
            {
                var value = rng.Next();
                Assert.True(value >= 0.0, $"Draw {i} was {value}, expected >= 0.");
                Assert.True(value < 1.0, $"Draw {i} was {value}, expected < 1.");
            }
        }

        [Fact]
        public void Next_AdvancesTheStream_SuccessiveDrawsDiffer()
        {
            var rng = new Mulberry32(42);

            var first = rng.Next();
            var second = rng.Next();

            Assert.NotEqual(first, second);
        }
    }
}
