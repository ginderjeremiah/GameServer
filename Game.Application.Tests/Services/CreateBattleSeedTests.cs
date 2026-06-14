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
    }
}
