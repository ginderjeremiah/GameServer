namespace Game.Api.Models.Progress
{
    /// <summary>
    /// Pushed to the client when a won battle accrues proficiency XP, so a level-up or milestone surfaces
    /// immediately rather than only after a refresh. Carries every proficiency the battle trained; the client
    /// updates its proficiency store and surfaces level-ups / milestones from it (the feedback itself lands in
    /// the client sub-issue). The live battle-completion path raises it; the offline batch does not (its gains
    /// ride the welcome-back summary).
    /// </summary>
    public class ProficiencyXpGainedModel : IModel
    {
        public required List<ProficiencyXpResultModel> Proficiencies { get; set; }

        /// <summary>The nodes this battle opened (a maxed tier's next tier, or a newly-satisfied gateway), so
        /// the client surfaces the unlock. Notification-only — opening grants no skill.</summary>
        public required List<ProficiencyOpenedModel> Opened { get; set; }
    }

    /// <summary>One proficiency's outcome from the battle: XP gained, the level/residual XP it ended at, the
    /// authored milestone levels the gain crossed, and the reward skills those milestones granted (a milestone
    /// in <see cref="MilestonesCrossed"/> absent from <see cref="GrantedSkillIds"/> is bonus-only).</summary>
    public class ProficiencyXpResultModel : IModel
    {
        public int ProficiencyId { get; set; }
        public decimal XpGained { get; set; }
        public int NewLevel { get; set; }
        public decimal NewXp { get; set; }
        public required List<int> MilestonesCrossed { get; set; }
        public required List<int> GrantedSkillIds { get; set; }
    }

    /// <summary>A node the battle opened (notification-only — opening grants no skill).</summary>
    public class ProficiencyOpenedModel : IModel
    {
        public int ProficiencyId { get; set; }
    }
}
