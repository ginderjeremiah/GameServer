namespace GameCore
{
    public class SharedProbabilityTable
    {
        private readonly List<List<ProbabilityData>?> _probabilityTables;
        private readonly List<int?> _aliases;
        private readonly Random _random;
        private readonly int _maxTables = 100;

        public SharedProbabilityTable()
        {
            _probabilityTables = [];
            _aliases = [];
            _random = new();
        }

        public SharedProbabilityTable(int maxTables) : this()
        {
            _maxTables = maxTables;
        }

        public bool HasProbabilities(int index)
        {
            return _probabilityTables.Count > index && _probabilityTables[index] != null;
        }

        public void AddProbabilities(IEnumerable<ProbabilityData> probabilities, int index)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(index, _maxTables);

            for (int i = _probabilityTables.Count; i <= index; i++)
            {
                _probabilityTables.Add(null);
            }

            _probabilityTables[index] = probabilities.ToList();
        }

        public void AddAliases(IEnumerable<AliasData> newAliases)
        {
            if (!newAliases.Any())
                return;

            for (int i = _aliases.Count; i <= newAliases.Max(alias => alias.Alias); i++)
            {
                _aliases.Add(null);
            }

            foreach (var alias in newAliases)
            {
                _aliases[alias.Alias] = alias.Value;
            }
        }

        public int GetRandomValue(int index)
        {
            var probabilities = (_probabilityTables.Count > index
                ? _probabilityTables[index]
                : null) ?? throw new ArgumentOutOfRangeException(nameof(index), $"Probability table at index '{index}' does not exist.");

            var rand = _random.NextSingle() * probabilities.Count;
            var randInt = (int)float.Floor(rand);
            var remainder = (decimal)(rand - randInt);
            var prob = probabilities[randInt];

            return remainder < prob.Probability ? prob.Value : _aliases[prob.Alias]
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

    public class ProbabilityData
    {
        public decimal Probability { get; set; }
        public int Value { get; set; }
        public int Alias { get; set; }
    }

    public class AliasData
    {
        public int Alias { get; set; }
        public int Value { get; set; }
    }
}
