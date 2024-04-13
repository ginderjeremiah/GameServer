namespace GameLibrary
{
    public class SharedProbabilityTable
    {
        private readonly List<List<IProbabilityData>?> probabilityTables;
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

        public void AddProbabilities(IEnumerable<IProbabilityData> probabilities, int index)
        {
            for (int i = probabilityTables.Count; i <= index; i++)
            {
                probabilityTables.Add(null);
            }
            probabilityTables[index] = probabilities.ToList();
        }

        public void AddAliases(IEnumerable<IAliasData> newAliases)
        {
            if (!newAliases.Any())
                return;

            for (int i = aliases.Count; i <= newAliases.Max(alias => alias.Alias); i++)
            {
                aliases.Add(null);
            }
            foreach (var alias in newAliases)
            {
                aliases[alias.Alias] = alias.Value;
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

    public class AliasNotFoundException : Exception
    {
        public AliasNotFoundException(int index)
            : base($"Alias does not exist at index: {index}")
        {
        }
    }

    public interface IProbabilityData
    {
        public decimal Probability { get; set; }
        public int Value { get; set; }
        public int Alias { get; set; }
    }

    public interface IAliasData
    {
        public int Alias { get; set; }
        public int Value { get; set; }
    }
}
