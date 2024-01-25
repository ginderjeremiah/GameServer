using DataAccess.Caches;
using DataAccess.Models.Items;
using DataAccess.Models.Skills;
using DataAccess.Models.Stats;
using System.Text.Json.Serialization;

namespace DataAccess.Models.Enemies
{
    public class Enemy
    {
        public List<ItemDrop> EnemyDrops { get; set; }
        [JsonIgnore]
        public BaseStatDistribution StatDistribution { get; set; }
        public string EnemyName { get; set; }
        public int EnemyId { get; set; }
        public List<int> SelectedSkills { get; set; }

        public List<Skill> GetSkills(ISkillCache skillCache)
        {
            return SelectedSkills.Select(skillCache.GetSkill).ToList();
        }

        public Enemy(int enemyId, string enemyName, BaseStatDistribution stats, List<ItemDrop> enemyDrops, List<int> selectedSkills)
        {
            EnemyDrops = enemyDrops;
            StatDistribution = stats;
            EnemyName = enemyName;
            EnemyId = enemyId;
            SelectedSkills = selectedSkills;
        }
    }
}
