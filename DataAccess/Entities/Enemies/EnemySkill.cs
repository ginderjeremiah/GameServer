using GameCore;
using GameCore.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.Enemies
{
    internal class EnemySkill : IEntity
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
