namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// A change set against the effects collection of a single skill, keyed by the skill's <see cref="Id"/>.
    /// </summary>
    public class SetSkillEffectsData
    {
        public int Id { get; set; }
        public required List<Change<SkillEffect>> Changes { get; set; }
    }
}
