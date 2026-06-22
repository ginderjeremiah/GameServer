namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// A prerequisite edge in the proficiency tree: <see cref="ProficiencyId"/> opens once every one of its
    /// prerequisite proficiencies is maxed (the open logic lands in a later sub-issue; this entity stores the
    /// edges only). Keyed by (proficiency, prerequisite).
    /// </summary>
    public class ProficiencyPrerequisite
    {
        public int ProficiencyId { get; set; }
        public int PrerequisiteProficiencyId { get; set; }

        public virtual Proficiency Proficiency { get => field ?? throw new NotLoadedException(nameof(Proficiency)); set; }
        public virtual Proficiency Prerequisite { get => field ?? throw new NotLoadedException(nameof(Prerequisite)); set; }
    }
}
