using DataAccess.Models.Attributes;
using DataAccess.Models.Items;
using System.Text.Json.Serialization;

namespace DataAccess.Models.Enemies
{
    public class Enemy
    {
        public List<ItemDrop> EnemyDrops { get; set; }
        [JsonIgnore]
        public List<AttributeDistribution> AttributeDistribution { get; set; }
        public string EnemyName { get; set; }
        public int EnemyId { get; set; }
        public List<int> SelectedSkills { get; set; }

        public Enemy(int enemyId, string enemyName, List<AttributeDistribution> attributeDist, List<ItemDrop> enemyDrops, List<int> selectedSkills)
        {
            EnemyDrops = enemyDrops;
            AttributeDistribution = attributeDist;
            EnemyName = enemyName;
            EnemyId = enemyId;
            SelectedSkills = selectedSkills;
        }
    }
}
