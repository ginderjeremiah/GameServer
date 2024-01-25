using GameLibrary;
using GameServer.BattleSimulation;

namespace GameServer.Models.Common
{
    public class EnemyInstance
    {
        public int EnemyId { get; set; }
        public int EnemyLevel { get; set; }
        public BattleBaseStats Stats { get; set; }
        public uint Seed { get; set; }

        public string Hash()
        {
            var data = $"{EnemyId}{EnemyLevel}{Stats.GetStatsString()}";
            return data.Hash(Seed.ToString(), 1);
        }
    }
}
