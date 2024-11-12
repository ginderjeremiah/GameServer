using Game.Core.Probability;

namespace Game.Core.Tests.Probability
{
    [TestClass]
    public class ProbabilityTableTests
    {
        [TestMethod]
        public void EmptyInitializationList_ThrowsException()
        {
            Assert.ThrowsException<ArgumentException>(() => new ProbabilityTable<int>([]));
        }

        [TestMethod]
        public void InitializationListHasNegativeWeightElement_ThrowsException()
        {
            List<WeightedValue<int>> list = [new WeightedValue<int>(0, 1), new WeightedValue<int>(1, -1)];
            Assert.ThrowsException<ArgumentException>(() => new ProbabilityTable<int>(list));
        }

        [TestMethod]
        public void SingleInitializationList_ProducesValueFromList()
        {
            List<WeightedValue<int>> list = [new WeightedValue<int>(0, 1)];
            var table = new ProbabilityTable<int>(list);
            var randomValue = table.GetRandomValue();

            Assert.AreEqual(list[0].Value, randomValue);
        }

        [TestMethod]
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

        [TestMethod]
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
                Assert.IsTrue(sampleProbability - actualProbability is > -0.01 and < 0.01);
            }
        }
    }
}
