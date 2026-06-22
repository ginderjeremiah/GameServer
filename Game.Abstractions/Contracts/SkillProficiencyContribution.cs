namespace Game.Abstractions.Contracts
{
    /// <summary>Read/authoring contract for one skill's contribution weight toward a proficiency.</summary>
    public class SkillProficiencyContribution : IModel
    {
        public int SkillId { get; set; }
        public decimal Weight { get; set; }
    }
}
