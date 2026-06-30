namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// A change set against the damage-portion collection of a single skill, keyed by the skill's
    /// <see cref="Id"/>. Each portion is keyed by its damage type (spike #1343).
    /// </summary>
    public class SetSkillPortionsData
    {
        public int Id { get; set; }
        public required List<Change<SkillDamagePortion>> Changes { get; set; }
    }
}
