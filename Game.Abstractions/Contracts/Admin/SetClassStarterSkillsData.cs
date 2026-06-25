namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>The full set of starter skills to associate with a single class (<see cref="ClassId"/>).</summary>
    public class SetClassStarterSkillsData
    {
        public int ClassId { get; set; }

        public required List<int> SkillIds { get; set; }
    }
}
