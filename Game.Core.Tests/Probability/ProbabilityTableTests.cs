using Game.Core.Probability;
using Xunit;

namespace Game.Core.Tests.Probability
{
    public class ProbabilityTableTests
    {
        [Fact]
        public void EmptyInitializationList_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() => new ProbabilityTable<int>([]));
        }

        [Fact]
        public void InitializationListHasNegativeWeightElement_ThrowsException()
        {
            List<WeightedValue<int>> list = [new WeightedValue<int>(0, 1), new WeightedValue<int>(1, -1)];
            Assert.Throws<ArgumentException>(() => new ProbabilityTable<int>(list));
        }

        [Fact]
        public void SingleInitializationList_ProducesValueFromList()
        {
            List<WeightedValue<int>> list = [new WeightedValue<int>(0, 1)];
            var table = new ProbabilityTable<int>(list);
            var randomValue = table.GetRandomValue();

            Assert.Equal(list[0].Value, randomValue);
        }

        [Fact]
        public void SingleZeroWeightList_ProducesValueFromList()
        {
            List<WeightedValue<int>> list = [new WeightedValue<int>(42, 0)];
            var table = new ProbabilityTable<int>(list);

            Assert.Equal(42, table.GetRandomValue());
        }

        [Fact]
        public void AllZeroWeights_ProducesUniformDistribution()
        {
            // An all-zero set makes the average weight zero; entries should fall back to uniform.
            List<WeightedValue<int>> list = [
                new WeightedValue<int>(0, 0),
                new WeightedValue<int>(1, 0),
                new WeightedValue<int>(2, 0),
            ];
            var table = new ProbabilityTable<int>(list);
            var buckets = list.Select(x => 0L).ToList();
            var iterations = 300_000;

            for (int i = 0; i < iterations; i++)
            {
                buckets[table.GetRandomValue()]++;
            }

            for (int i = 0; i < buckets.Count; i++)
            {
                var sampleProbability = (double)buckets[i] / iterations;
                Assert.True(Math.Abs(sampleProbability - (1.0 / buckets.Count)) < 0.01);
            }
        }

        [Theory]
        [MemberData(nameof(UnevenWeightSets))]
        public void UnevenlyDividingWeights_AlwaysProduceValidIndex(WeightedValue<int>[] entries)
        {
            // Unevenly-dividing weights previously risked leaving an unpopulated _values slot,
            // which would surface as a NullReferenceException or an out-of-range alias lookup.
            var table = new ProbabilityTable<int>([.. entries]);
            var validValues = entries.Select(x => x.Value).ToHashSet();

            for (int i = 0; i < 500_000; i++)
            {
                var value = table.GetRandomValue();
                Assert.Contains(value, validValues);
            }
        }

        [Theory]
        [MemberData(nameof(UnevenWeightSets))]
        public void UnevenlyDividingWeights_ProduceReasonableDistribution(WeightedValue<int>[] entries)
        {
            var list = entries.ToList();
            var table = new ProbabilityTable<int>(list);
            var buckets = list.Select(x => 0L).ToList();
            var iterations = 1_000_000;

            for (int i = 0; i < iterations; i++)
            {
                buckets[table.GetRandomValue()]++;
            }

            AssertDistributionIsReasonable(list, buckets);
        }

        public static IEnumerable<object[]> UnevenWeightSets()
        {
            // Three thirds: 1/3 cannot be represented exactly, so residual weights never land on 1.0.
            yield return new object[]
            {
                new[]
                {
                    new WeightedValue<int>(0, 1),
                    new WeightedValue<int>(1, 1),
                    new WeightedValue<int>(2, 1),
                },
            };
            // Prime weights over a prime total produce repeating-decimal normalized weights.
            yield return new object[]
            {
                new[]
                {
                    new WeightedValue<int>(0, 1),
                    new WeightedValue<int>(1, 2),
                    new WeightedValue<int>(2, 4),
                    new WeightedValue<int>(3, 7),
                    new WeightedValue<int>(4, 9),
                    new WeightedValue<int>(5, 11),
                },
            };
            // A heavy skew mixed with several tiny weights stresses the small/big rebalancing.
            yield return new object[]
            {
                new[]
                {
                    new WeightedValue<int>(0, 997),
                    new WeightedValue<int>(1, 1),
                    new WeightedValue<int>(2, 1),
                    new WeightedValue<int>(3, 1),
                },
            };
            // Zero-weight entries should never be returned but must not break construction.
            yield return new object[]
            {
                new[]
                {
                    new WeightedValue<int>(0, 0),
                    new WeightedValue<int>(1, 3),
                    new WeightedValue<int>(2, 0),
                    new WeightedValue<int>(3, 5),
                },
            };
        }

        [Fact]
        public void MultiInitializationList_ProducesReasonableDistribution()
        {
            var list = SetupMultiElementList();
            var table = new ProbabilityTable<int>(list);
            var buckets = list.Select(x => 0L).ToList();
            var iterations = 1_000_000;

            for (int i = 0; i < iterations; i++)
            {
                buckets[table.GetRandomValue()]++;
            }

            AssertDistributionIsReasonable(list, buckets);
        }

        [Fact]
        public void MultiRandomInitializationList_ProducesReasonableDistribution()
        {
            var list = SetupMultiRandomElementList();
            var table = new ProbabilityTable<int>(list);
            var buckets = list.Select(x => 0L).ToList();
            var iterations = 1_000_000;

            for (int i = 0; i < iterations; i++)
            {
                buckets[table.GetRandomValue()]++;
            }

            AssertDistributionIsReasonable(list, buckets);
        }

        [Fact]
        public async Task ConcurrentGetRandomValue_StaysValidAndNonDegenerate()
        {
            // ProbabilityTable instances are cached in process-wide static collections (e.g. the per-zone
            // enemy spawn tables) and drawn from concurrently, so GetRandomValue must be safe to call from
            // many threads at once — the reason it now draws from the thread-safe Random.Shared.
            //
            // This is a best-effort concurrency guard, not a deterministic detector of the fix: it hammers
            // the table from every core and asserts the draws stay in range, complete without throwing, and
            // keep an even spread (every value drawn, none dominating). On .NET's modern unseeded Random
            // (xoshiro256**, used by new Random() on this target) a data race over the shared instance is
            // undefined behaviour but degrades gracefully on typical hardware rather than collapsing the
            // distribution, so this would not reliably fail against the old per-instance RNG. It instead
            // documents and locks in the concurrent contract and catches gross regressions.
            var valueCount = 10;
            var list = Enumerable.Range(0, valueCount)
                .Select(value => new WeightedValue<int>(value, 1))
                .ToList();
            var table = new ProbabilityTable<int>(list);

            var buckets = new long[valueCount];
            var iterationsPerTask = 100_000;
            var taskCount = Math.Max(Environment.ProcessorCount, 4);

            using var startGate = new ManualResetEventSlim(false);
            var tasks = Enumerable.Range(0, taskCount)
                .Select(_ => Task.Run(() =>
                {
                    // Block every task until all are spun up so the draws actually overlap.
                    startGate.Wait();
                    for (int i = 0; i < iterationsPerTask; i++)
                    {
                        var value = table.GetRandomValue();
                        Assert.InRange(value, 0, valueCount - 1);
                        Interlocked.Increment(ref buckets[value]);
                    }
                }))
                .ToArray();

            startGate.Set();
            await Task.WhenAll(tasks);

            // Every value must be drawn at least once; a degenerate run collapses onto a single bucket.
            Assert.All(buckets, count => Assert.True(count > 0, "Expected every value to be drawn at least once."));

            // And with equal weights no single value should dominate far beyond its ~1/valueCount share.
            var totalDraws = buckets.Sum();
            var maxShare = buckets.Max() / (double)totalDraws;
            Assert.True(maxShare < 0.5, $"Distribution is degenerate; one value took {maxShare:P0} of draws.");
        }

        private static List<WeightedValue<int>> SetupMultiElementList()
        {
            // total weight is 45 so the last element is exactly equal 1/5 probability.
            // this is important because it should in theory bypass the big and small lists
            // and go directly into the values list with a probability of 1.
            return [
                new WeightedValue<int>(0, 0),
                new WeightedValue<int>(1, 7),
                new WeightedValue<int>(2, 24),
                new WeightedValue<int>(3, 5),
                new WeightedValue<int>(4, 9),
            ];
        }

        private static List<WeightedValue<int>> SetupMultiRandomElementList()
        {
            var random = new Random();

            return Enumerable.Range(0, 20)
                .Select(i => new WeightedValue<int>(i, random.Next(1, 1000)))
                .ToList();
        }

        private static void AssertDistributionIsReasonable(List<WeightedValue<int>> list, List<long> buckets)
        {
            var totalWeight = list.Sum(x => x.Weight);
            var iterations = buckets.Sum();
            for (int i = 0; i < buckets.Count; i++)
            {
                var actualProbability = (double)list[i].Weight / totalWeight;
                var sampleProbability = (double)buckets[i] / iterations;
                Assert.True(sampleProbability - actualProbability is > -0.01 and < 0.01);
            }
        }
    }
}
