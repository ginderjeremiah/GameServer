namespace Game.Abstractions.Contracts
{
    /// <summary>Read/authoring contract for a skill a proficiency grants at a given level (the milestone payout).</summary>
    public class ProficiencyLevelReward : IModel
    {
        public int Level { get; set; }
        public int RewardSkillId { get; set; }
    }
}
