namespace GameLibrary
{
    public class SharedProbabilityTable
    {
        private readonly List<List<ProbabilityData>?> probabilityTables;
        private readonly List<int?> aliases;
        private readonly Random random;
        public SharedProbabilityTable()
        {
            probabilityTables = new();
            aliases = new();
            random = new();
        }

        public bool HasProbabilities(int index)
        {
            return probabilityTables.Count > index && probabilityTables[index] != null;
        }

        public void AddProbabilities(List<ProbabilityData> probabilities, int index)
        {
            for (int i = probabilityTables.Count; i <= index; i++)
            {
                probabilityTables.Add(null);
            }
            probabilityTables[index] = probabilities;
        }

        public void AddAliases(List<(int, int)> newAliases)
        {
            if (newAliases.Count == 0)
                return;

            for (int i = aliases.Count; i <= newAliases.Max(alias => alias.Item1); i++)
            {
                aliases.Add(null);
            }
            foreach (var alias in newAliases)
            {
                aliases[alias.Item1] = alias.Item2;
            }
        }

        public int GetRandomValue(int index)
        {
            var probabilities = (probabilityTables.Count > index
                ? probabilityTables[index]
                : null) ?? throw new ArgumentOutOfRangeException(nameof(index), $"Probability table at index '{index}' does not exist.");

            var rand = random.NextSingle() * probabilities.Count;
            var randInt = (int)float.Floor(rand);
            var remainder = (decimal)(rand - randInt);
            var prob = probabilities[randInt];

            return remainder < prob.Probability ? prob.Value : aliases[prob.Alias]
                ?? throw new AliasNotFoundException(prob.Alias);

        }
    }

    public class ProbabilityData
    {
        public decimal Probability { get; set; }
        public int Value { get; set; }
        public int Alias { get; set; }
    }

    public class AliasNotFoundException : Exception
    {
        public AliasNotFoundException(int index)
            : base($"Alias does not exist at index: {index}")
        {
        }
    }
}
