namespace Game.Core.Probability
{
    /// <summary>
    /// A class for generating a random value based on a collection of values with probability weights.
    /// </summary>
    public class ProbabilityTable<T>
    {
        // Tolerance used when classifying normalized weights so that values which are
        // mathematically equal to the average but land just off 1.0 due to floating-point
        // rounding are still treated as "exactly average" rather than small/big.
        private const double Epsilon = 1e-9;

        private readonly ProbabilityData<T>[] _values;
        private readonly T[] _aliases;

        /// <summary>
        /// Generates a probability table based on a collection of <see cref="WeightedValue{T}"/> objects
        /// using Vose's alias method.
        /// </summary>
        /// <param name="values">The weighted values to build the table from.</param>
        public ProbabilityTable(IEnumerable<WeightedValue<T>> values)
        {
            // Convert to list to avoid repeated enumeration of a potential linq expression.
            var valueList = values.ToList();

            if (valueList.Count <= 0)
            {
                throw new ArgumentException($"{nameof(values)} contains no elements.", nameof(values));
            }

            _values = new ProbabilityData<T>[valueList.Count];
            _aliases = new T[valueList.Count];
            var averageWeight = valueList.Average(x => x.Weight);

            // Lists hold values whose normalized weight is below/at-or-above the average (1.0).
            var smallList = new List<NormalizedWeightedValue<T>>(valueList.Count);
            var bigList = new List<NormalizedWeightedValue<T>>(valueList.Count);

            foreach (var value in valueList)
            {
                if (value.Weight < 0)
                {
                    throw new ArgumentException($"{nameof(values)} cannot contain an element with a negative weight.", nameof(values));
                }

                // When every weight is zero the average is zero; treat all entries as uniform (weight 1.0).
                var weight = averageWeight == 0 ? 1.0 : value.Weight / averageWeight;
                if (weight < 1.0 - Epsilon)
                {
                    smallList.Add(new(value.Value, weight));
                }
                else
                {
                    bigList.Add(new(value.Value, weight));
                }
            }

            var valuesIndex = 0;
            var aliasesIndex = 0;

            // While both lists have entries, pair a small entry with a big one to fill a full column.
            while (smallList.Count > 0 && bigList.Count > 0)
            {
                var small = smallList[^1];
                smallList.RemoveAt(smallList.Count - 1);
                var big = bigList[^1];
                bigList.RemoveAt(bigList.Count - 1);

                _values[valuesIndex] = new ProbabilityData<T>(small.Value, small.NormalizedWeight, aliasesIndex);
                valuesIndex++;
                _aliases[aliasesIndex] = big.Value;
                aliasesIndex++;

                // Reduce the big entry by the portion donated to the small column and requeue it.
                big.NormalizedWeight -= 1.0 - small.NormalizedWeight;
                if (big.NormalizedWeight < 1.0 - Epsilon)
                {
                    smallList.Add(big);
                }
                else
                {
                    bigList.Add(big);
                }
            }

            // Any remaining entries occupy a full column on their own (alias unused).
            foreach (var big in bigList)
            {
                _values[valuesIndex] = new ProbabilityData<T>(big.Value, 1.0, -1);
                valuesIndex++;
            }

            // Leftover small entries arise only from floating-point residue; they also fill a full column.
            foreach (var small in smallList)
            {
                _values[valuesIndex] = new ProbabilityData<T>(small.Value, 1.0, -1);
                valuesIndex++;
            }
        }

        /// <summary>
        /// Gets a random <typeparamref name="T"/> based on the weight distribution of the probability table.
        /// </summary>
        /// <remarks>
        /// Uses the thread-safe <see cref="Random.Shared"/> because table instances are cached in
        /// process-wide static collections (e.g. per-zone enemy spawn tables) and drawn from concurrently.
        /// </remarks>
        /// <returns>A random <typeparamref name="T"/>.</returns>
        public T GetRandomValue()
        {
            var rand = Random.Shared.NextDouble() * _values.Length;
            // Clamp to a valid index in case NextDouble() returns a value close enough to 1.0
            // that the scaled product rounds up to _values.Length.
            var index = Math.Clamp((int)double.Floor(rand), 0, _values.Length - 1);
            var remainder = rand - index;
            var data = _values[index];

            // Use the alias only when the draw falls into the donated portion and an alias exists.
            return remainder < data.Probability || data.AliasIndex < 0
                ? data.Value
                : _aliases[data.AliasIndex];
        }
    }
}
