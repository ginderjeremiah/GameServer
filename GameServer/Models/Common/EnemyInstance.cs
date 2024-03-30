using DataAccess.Models.PlayerAttributes;
using GameLibrary;

namespace GameServer.Models.Common
{
    public class EnemyInstance
    {
        public int EnemyId { get; set; }
        public int EnemyLevel { get; set; }
        public List<PlayerAttribute> Attributes { get; set; }
        public uint Seed { get; set; }

        public string Hash()
        {
            var data = $"{EnemyId}{EnemyLevel}{string.Join(",", Attributes.Select(att => (double)att.Amount))}";
            return data.Hash(Seed.ToString(), 1);
        }
    }
}
