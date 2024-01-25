using System.Text.Json.Serialization;

namespace GameServer.BattleSimulation
{
    public class BattleBaseStats
    {
        private readonly Dictionary<string, int> _stats = new();

        public int this[string key]
        {
            get => _stats[key.ToLower()];
            set => _stats[key.ToLower()] = value;
        }

        public int Strength { get => _stats["strength"]; set => _stats["strength"] = value; }
        public int Endurance { get => _stats["endurance"]; set => _stats["endurance"] = value; }
        public int Intellect { get => _stats["intellect"]; set => _stats["intellect"] = value; }
        public int Agility { get => _stats["agility"]; set => _stats["agility"] = value; }
        public int Dexterity { get => _stats["dexterity"]; set => _stats["dexterity"] = value; }
        public int Luck { get => _stats["luck"]; set => _stats["luck"] = value; }

        [JsonIgnore]
        public int Total
        {
            get
            {
                return _stats.Sum(stat => stat.Value);
            }
        }

        public string GetStatsString()
        {
            return string.Join("", _stats.Select(x => x.ToString()));
        }

        public bool ChangeStats(BattleBaseStats changedStats)
        {
            if (changedStats._stats.All(kvp => _stats[kvp.Key] + kvp.Value > 0))
            {
                foreach (var statKvp in changedStats._stats)
                {
                    _stats[statKvp.Key] += statKvp.Value;
                }
                return true;
            }
            return false;
        }
    }
}
