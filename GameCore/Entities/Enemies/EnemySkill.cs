using System.Data;

namespace GameCore.Entities.Enemies
{
    public class EnemySkill : IEntity
    {
        public int EnemyId { get; set; }
        public int SkillId { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            EnemyId = record["EnemyId"].AsInt();
            SkillId = record["SkillId"].AsInt();
        }
    }
}
