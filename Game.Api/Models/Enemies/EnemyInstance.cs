using Game.Api.Models.Attributes;

namespace Game.Api.Models.Enemies
{
    public class EnemyInstance : IModel
    {
        public int Id { get; set; }
        public int Level { get; set; }
        public IEnumerable<BattlerAttribute> Attributes { get; set; }
        public uint Seed { get; set; }
        public List<int> SelectedSkills { get; set; }

        public EnemyInstance() { }

        public EnemyInstance(Core.Battle.EnemyInstance enemyInstance)
        {
            Id = enemyInstance.Id;
            Level = enemyInstance.Level;
            Attributes = enemyInstance.Attributes.To().Model<BattlerAttribute>();
            Seed = enemyInstance.Seed;
            SelectedSkills = enemyInstance.SelectedSkills;
        }
    }
}
