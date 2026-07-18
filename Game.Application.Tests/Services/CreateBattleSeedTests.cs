using Game.Application.Services;
using Xunit;

namespace Game.Application.Tests.Services
{
    /// <summary>
    /// Unit tests pinning the battle RNG seed generation (<see cref="BattleService.CreateBattleSeed"/>).
    /// The seed is the shared starting point for the seeded crit/dodge/block work (#178). It is now drawn
    /// from a cryptographic (non-time) entropy source rather than folded from <c>DateTime.Ticks</c> (#653),
    /// so the contract is no longer a fixed value vector — the value is non-deterministic by design and is
    /// not a function of any passed-in time. The cross-port RNG parity itself lives in
    /// <c>Game.Core.Tests/Battle/Mulberry32ParityTests.cs</c>; the seed is server-derived and transmitted to
    /// the client, so there is no frontend seed-derivation to mirror — the client consumes the seed it receives.
    /// </summary>
    public class CreateBattleSeedTests
    {
        // The generator takes no input (it cannot be a function of wall-clock time) and yields varied values.
        // A monotonic, low-entropy source (e.g. the old DateTime.Ticks fold) called in a tight loop would
        // collapse to a single distinct value here; a CSPRNG effectively never does over this sample size.
        [Fact]
        public void CreateBattleSeed_ProducesVariedNonTimeSeeds()
        {
            var seeds = Enumerable.Range(0, 1000)
                .Select(_ => BattleService.CreateBattleSeed())
                .ToList();

            Assert.True(seeds.Distinct().Count() > 1,
                "Expected the seed generator to produce varied values from a non-time entropy source.");
        }

        // #2112: a fresh seed equal to the player's LastCreditedBattleSeed must never be handed out, since
        // BattleAlreadyCredited would then treat the brand-new battle as a stale re-presentation of the
        // already-credited one. The real CSPRNG can't be made to collide on demand (2^-32), so this pins the
        // public entry point's exclude plumbing statistically, and DrawSeed below pins the redraw loop itself
        // deterministically via a stubbed generator.
        [Fact]
        public void CreateBattleSeed_NeverEqualsProvidedExclude()
        {
            var exclude = BattleService.CreateBattleSeed();

            var seeds = Enumerable.Range(0, 1000)
                .Select(_ => BattleService.CreateBattleSeed(exclude))
                .ToList();

            Assert.DoesNotContain(exclude, seeds);
        }

        [Fact]
        public void DrawSeed_RedrawsUntilPastTheExcludedValue()
        {
            var draws = new Queue<uint>([5u, 5u, 5u, 7u]);

            var result = BattleService.DrawSeed(draws.Dequeue, exclude: 5u);

            Assert.Equal(7u, result);
            Assert.Empty(draws);
        }

        [Fact]
        public void DrawSeed_NullExclude_ReturnsFirstDrawUnconditionally()
        {
            var draws = new Queue<uint>([5u, 7u]);

            var result = BattleService.DrawSeed(draws.Dequeue, exclude: null);

            Assert.Equal(5u, result);
            Assert.Equal(7u, draws.Peek());
        }
    }
}
