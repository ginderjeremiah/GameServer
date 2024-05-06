using GameServer.Models.Attributes;

namespace GameServer.Models.Enemies
{
    public class EnemyInstance : IModel
    {
        public int EnemyId { get; set; }
        public int Level { get; set; }
        public List<BattlerAttribute> Attributes { get; set; }
        public uint Seed { get; set; }
        public List<int> SelectedSkills { get; set; }

        public EnemyInstance() { }

        public EnemyInstance(GameCore.BattleSimulation.EnemyInstance enemyInstance)
        {
            EnemyId = enemyInstance.EnemyId;
            Level = enemyInstance.Level;
            Attributes = enemyInstance.Attributes.Select(att => new BattlerAttribute(att)).ToList();
            Seed = enemyInstance.Seed;
            SelectedSkills = enemyInstance.SelectedSkills;
        }
    }
}
