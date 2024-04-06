using GameLibrary;
using GameServer.Models.Attributes;

namespace GameServer.Models.Enemies
{
    public class EnemyInstance
    {
        public int EnemyId { get; set; }
        public int EnemyLevel { get; set; }
        public List<BattlerAttribute> Attributes { get; set; }
        public uint Seed { get; set; }

        public string Hash()
        {
            var data = $"{EnemyId}{EnemyLevel}{string.Join(",", Attributes.Select(att => (double)att.Amount))}";
            return data.Hash(Seed.ToString(), 1);
        }
    }
}
