namespace Game.Core.Probability
{
    /// <summary>
    /// A class for generating a random value based on a collection of values with probability weights.
    /// </summary>
    public class ProbabilityTable<T>
    {
        private readonly ProbabilityData<T>[] _values;
        private readonly T[] _aliases;
        private readonly Random _random = new();

        /// <summary>
        /// Generates a probability table based on a collection of <see cref="WeightedValue{T}"/> objects.
        /// </summary>
        /// <param name="values"></param>
        public ProbabilityTable(IEnumerable<WeightedValue<T>> values)
        {
            // Convert to list to avoid repeated enumeration of a potential linq expression
            var valueList = values.ToList();

            if (valueList.Count <= 0)
            {
                throw new ArgumentException($"{nameof(values)} contains no elements.", nameof(values));
            }

            _values = new ProbabilityData<T>[valueList.Count];
            _aliases = new T[_values.Length];
            var valuesCurrentIndex = 0;
            var aliasesCurrentIndex = 0;
            var averageWeight = valueList.Average(x => x.Weight);
            var smallList = new List<NormalizedWeightedValue<T>>(valueList.Count); //List to hold values where probability is less than average;
            var bigList = new List<NormalizedWeightedValue<T>>(valueList.Count); //List to hold values where probability is greater than average;

            // sort each value into either the small list, big list, or final list (_values) based on weight relative to average;
            foreach (var value in valueList)
            {
                if (value.Weight < 0)
                {
                    throw new ArgumentException($"{nameof(values)} cannot contain an element with a negative weight.", nameof(values));
                }

                var weight = value.Weight / averageWeight;
                if (weight < 1)
                {
                    smallList.Add(new(value.Value, weight));
                }
                else if (weight == 1.0)
                {
                    _values[valuesCurrentIndex] = new ProbabilityData<T>(value.Value, 1.0, -1);
                    valuesCurrentIndex++;
                }
                else
                {
                    bigList.Add(new(value.Value, weight));
                }
            }

            if (bigList.Count == 0)
            {
                return;
            }

            var bigIndex = 0;

            // For each element in the small list, fill its missing weight from an element in the bigList.
            // Don't use foreach because smallList will have items added to it as we take weight from items in the bigList.
            for (int smallIndex = 0; smallIndex < smallList.Count; smallIndex++)
            {
                if (bigIndex < bigList.Count)
                {
                    var currentBig = bigList[bigIndex];
                    var currentSmall = smallList[smallIndex];

                    // Remove portion of big probability to give to small.
                    currentBig.NormalizedWeight -= 1.0 - currentSmall.NormalizedWeight;

                    // Add new entry in probabilities table for small and alias for big.
                    _values[valuesCurrentIndex] = new ProbabilityData<T>(currentSmall.Value, currentSmall.NormalizedWeight, aliasesCurrentIndex);
                    valuesCurrentIndex++;
                    _aliases[aliasesCurrentIndex] = currentBig.Value;
                    aliasesCurrentIndex++;

                    // If big now has probability less than one than move to small list.
                    if (currentBig.NormalizedWeight < 1.0)
                    {
                        bigIndex++;
                        smallList.Add(currentBig);
                    }
                }
                else
                {
                    _values[valuesCurrentIndex] = new ProbabilityData<T>(smallList[smallIndex].Value, 1.0, -1);
                }
            }

            if (bigIndex < bigList.Count)
            {
                _values[valuesCurrentIndex] = new ProbabilityData<T>(bigList[bigIndex].Value, 1.0, -1);
            }
        }

        /// <summary>
        /// Gets a random <typeparamref name="T"/> based on the weight distribution of the probability table.
        /// </summary>
        /// <returns>A random <typeparamref name="T"/>.</returns>
        public T GetRandomValue()
        {
            var rand = _random.NextSingle() * _values.Length;
            var randInt = (int)float.Floor(rand);
            var remainder = rand - randInt;
            var data = _values[randInt];

            return remainder <= data.Probability ? data.Value : _aliases[data.AliasIndex];
        }
    }
}
