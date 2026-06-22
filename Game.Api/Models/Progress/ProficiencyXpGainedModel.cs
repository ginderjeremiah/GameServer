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
    }

    /// <summary>One proficiency's outcome from the battle: XP gained, the level/residual XP it ended at, and
    /// any authored milestone levels the gain crossed.</summary>
    public class ProficiencyXpResultModel : IModel
    {
        public int ProficiencyId { get; set; }
        public decimal XpGained { get; set; }
        public int NewLevel { get; set; }
        public decimal NewXp { get; set; }
        public required List<int> MilestonesCrossed { get; set; }
    }
}
