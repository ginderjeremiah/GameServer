using Game.Core.Battle;
using Xunit;

namespace Game.Core.Tests.Battle
{
    /// <summary>
    /// Cross-implementation parity vector for the seeded battle RNG primitive.
    /// Every seed/output row here MUST be mirrored — with identical seeds and identical
    /// expected draws — in the frontend suite
    /// <c>UI/src/tests/lib/engine/mulberry32-parity.test.ts</c>.
    /// <para>
    /// The two ports (<see cref="Mulberry32"/> here, <c>UI/src/lib/engine/mulberry32.ts</c>
    /// on the client) reach a bit-identical 32-bit stream through subtly different language
    /// semantics — C# native <c>uint</c> wrap-around and logical <c>&gt;&gt;</c> versus JS
    /// <c>Math.imul</c> and <c>&gt;&gt;&gt; 0</c> unsigned coercion. The frontend/backend battle
    /// replay (anti-cheat) depends on them agreeing once seeded crit/dodge effects (#178) start
    /// consuming <see cref="Mulberry32.Next"/>, so this vector locks the two ports together
    /// before anything depends on them.
    /// </para>
    /// </summary>
    public class Mulberry32ParityTests
    {
        /// <summary>
        /// 2^32 — the divisor the implementation uses to map a 32-bit draw into <c>[0, 1)</c>.
        /// Expected outputs are written as <c>numerator / Uint32Range</c>: because the divisor is
        /// a power of two and every numerator is a 32-bit unsigned integer, the quotient is an
        /// exact <see cref="double"/>, so both ports produce bit-identical values.
        /// </summary>
        private const double Uint32Range = 4294967296.0;

        /// <summary>
        /// The shared parity matrix: a fixed seed and the exact 32-bit numerators of the first
        /// six <see cref="Mulberry32.Next"/> draws it must produce. Seeds span the edge cases
        /// (zero, one) and high-bit values (a large arbitrary seed and one near <c>uint.MaxValue</c>)
        /// where the cross-language unsigned-shift/wrap semantics are most likely to diverge.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, (uint Seed, uint[] Numerators)> Vectors =
            new Dictionary<string, (uint, uint[])>
            {
                ["seedZero"] = (0u,
                    [1144304738, 1416247, 958946056, 627933444, 2007157716, 2340967985]),
                ["seedOne"] = (1u,
                    [2693262067, 11749833, 2265367787, 4213581821, 4159151403, 1207330352]),
                ["seedArbitrary"] = (123456789u,
                    [1107202814, 4169434471, 3372958138, 885470128, 1301683845, 3208624240]),
                ["seedHighBits"] = (0xDEADBEEFu,
                    [4043151706, 1147597007, 3315858022, 1538288752, 2042435954, 3600176436]),
            };

        public static IEnumerable<object[]> VectorNames =>
            Vectors.Keys.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(VectorNames))]
        public void Parity_Vector_MatchesExpectedSequence(string vectorName)
        {
            var (seed, numerators) = Vectors[vectorName];
            var rng = new Mulberry32(seed);

            foreach (var numerator in numerators)
            {
                Assert.Equal(numerator / Uint32Range, rng.Next());
            }
        }

        /// <summary>
        /// Number of draws taken before sampling the long-run tail. Chosen well past ~4.9M, the point
        /// where a JS double accumulating the seed (<c>initialSeed + N*0x6D2B79F5</c>) exceeds 2^53 and
        /// begins losing low bits — the latent divergence the frontend port guards against by truncating
        /// the seed to a uint32 each step. The fixed-length <see cref="Vectors"/> table can't catch it.
        /// </summary>
        private const int LongRunDraws = 6_000_000;

        private const uint LongRunSeed = 0xDEADBEEFu;

        /// <summary>
        /// The exact numerators of the final six draws of the <see cref="LongRunDraws"/>-long stream for
        /// <see cref="LongRunSeed"/>. MUST be mirrored in the frontend suite's long-run case.
        /// </summary>
        private static readonly uint[] LongRunTailNumerators =
            [2279814222, 2629024272, 2977756834, 4201168860, 3781448676, 52134473];

        [Fact]
        public void Parity_LongRun_MatchesExpectedTail()
        {
            var rng = new Mulberry32(LongRunSeed);
            var tailStart = LongRunDraws - LongRunTailNumerators.Length;

            for (var i = 0; i < tailStart; i++)
            {
                rng.Next();
            }

            foreach (var numerator in LongRunTailNumerators)
            {
                Assert.Equal(numerator / Uint32Range, rng.Next());
            }
        }
    }
}
