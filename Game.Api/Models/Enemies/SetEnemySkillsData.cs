namespace Game.Api.Models.Enemies
{
    public class SetEnemySkillsData
    {
        public int EnemyId { get; set; }

        public required List<int> SkillIds { get; set; }
    }
}
