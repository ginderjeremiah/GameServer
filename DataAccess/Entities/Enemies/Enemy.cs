using DataAccess.Entities.Drops;
using GameLibrary;
using GameLibrary.Database.Interfaces;
using System.Data;

namespace DataAccess.Entities.Enemies
{
    public class Enemy : IEntity
    {
        public int EnemyId { get; set; }
        public string EnemyName { get; set; }
        public List<AttributeDistribution> AttributeDistribution { get; set; }
        public List<ItemDrop> EnemyDrops { get; set; }
        public List<int> SkillPool { get; set; }

        public void LoadFromReader(IDataRecord record)
        {
            EnemyId = record["EnemyId"].AsInt();
            EnemyName = record["EnemyName"].AsString();
        }
    }
}
