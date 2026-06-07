namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>The full skill pool to associate with a single enemy (<see cref="EnemyId"/>).</summary>
    public class SetEnemySkillsData
    {
        public int EnemyId { get; set; }

        public required List<int> SkillIds { get; set; }
    }
}
