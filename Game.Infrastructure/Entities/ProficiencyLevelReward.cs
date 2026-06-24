namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// A skill granted when a proficiency reaches a given level (the "milestone" payout), mirroring
    /// <see cref="Item.GrantedSkillId"/> — the id is the only persisted link. Sparse — only levels that grant
    /// a skill have a row, and a level grants at most one. Keyed by (proficiency, level).
    /// </summary>
    public class ProficiencyLevelReward
    {
        public int ProficiencyId { get; set; }
        public int Level { get; set; }
        public int RewardSkillId { get; set; }

        public virtual Proficiency Proficiency { get => field ?? throw new NotLoadedException(nameof(Proficiency)); set; }
        public virtual Skill RewardSkill { get => field ?? throw new NotLoadedException(nameof(RewardSkill)); set; }
    }
}
