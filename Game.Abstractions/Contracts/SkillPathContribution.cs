namespace Game.Abstractions.Contracts
{
    /// <summary>Read/authoring contract for one skill's contribution to a path: the home tier it is native to
    /// and its weight (the owning path is the enclosing <see cref="Path"/>).</summary>
    public class SkillPathContribution : IModel
    {
        public int SkillId { get; set; }
        public int HomeTier { get; set; }
        public decimal Weight { get; set; }
    }
}
