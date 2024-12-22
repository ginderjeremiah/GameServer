namespace Game.Api.Models.Enemies
{
    public class SetEnemySkillsData
    {
        public int EnemyId { get; set; }

        public List<int> SkillIds { get; set; }
    }
}
