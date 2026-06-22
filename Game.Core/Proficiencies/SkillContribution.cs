namespace Game.Core.Proficiencies
{
    /// <summary>
    /// One path a skill feeds: the path it contributes to, the <see cref="HomeTier"/> (path ordinal) it is
    /// native to, and its contribution weight. Produced as the reverse index (skill → contributions) bundled
    /// in the proficiency cache snapshot. The battle XP path routes each contribution to the path's current
    /// frontier tier and discounts it by the falloff over the home-tier→frontier distance.
    /// </summary>
    public class SkillContribution
    {
        public required int PathId { get; init; }
        public required int HomeTier { get; init; }
        public required double Weight { get; init; }
    }
}
