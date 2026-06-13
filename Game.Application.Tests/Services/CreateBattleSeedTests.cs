using Game.Application.Services;
using Xunit;

namespace Game.Application.Tests.Services
{
    /// <summary>
    /// Unit tests pinning the battle RNG seed derivation (<see cref="BattleService.CreateBattleSeed"/>).
    /// The seed is the shared starting point for the seeded crit/dodge/block work (#178) and must fold the
    /// 64-bit tick count into uint32 by plain truncation (its low 32 bits), not a biased modulo by
    /// <c>uint.MaxValue</c> (2^32 - 1), which can never yield <c>uint.MaxValue</c> and does not cleanly
    /// truncate. The cross-port RNG parity itself lives in
    /// <c>Game.Core.Tests/Battle/Mulberry32ParityTests.cs</c>; the seed is server-derived and transmitted to
    /// the client, so there is no frontend seed-derivation to mirror — the client consumes the seed it receives.
    /// </summary>
    public class CreateBattleSeedTests
    {
        // ticks => expected seed (the low 32 bits of the 64-bit tick count). The chosen vectors expose the
        // old biased fold: under "% uint.MaxValue" the uint.MaxValue case yielded 0 and the 2^32 case yielded
        // 1 — both wrong.
        [Theory]
        [InlineData(0L, 0u)]
        [InlineData(4294967295L, 4294967295u)]          // uint.MaxValue ticks: a value "% uint.MaxValue" can never produce
        [InlineData(4294967296L, 0u)]                   // 2^32 ticks folds cleanly to 0, not 1
        [InlineData(639169488000000000L, 1266819072u)]  // 2026-06-13T12:00:00Z — a realistic battle-start time
        [InlineData(773437014593704226L, 4009693474u)]  // high-bit arbitrary tick count
        public void CreateBattleSeed_TruncatesTicksToLow32Bits(long ticks, uint expectedSeed)
        {
            var seed = BattleService.CreateBattleSeed(new DateTime(ticks, DateTimeKind.Utc));

            Assert.Equal(expectedSeed, seed);
        }
    }
}
