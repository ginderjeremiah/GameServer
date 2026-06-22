namespace Game.Core.Proficiencies
{
    /// <summary>
    /// A proficiency's payout on reaching a given level: the attribute bonus(es) granted at that level and an
    /// optional skill unlocked there. A player's total proficiency bonus is the sum of the increments for
    /// every level they have reached (see <c>docs/spikes/982-proficiency-system.md</c>).
    /// </summary>
    public class ProficiencyLevel
    {
        public required int Level { get; init; }
        public required IReadOnlyList<ProficiencyModifier> Modifiers { get; init; }
        public required int? RewardSkillId { get; init; }
    }
}
