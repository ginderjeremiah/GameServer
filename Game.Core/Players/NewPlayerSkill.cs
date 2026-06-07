namespace Game.Core.Players
{
    /// <summary>
    /// A starter skill granted to a freshly created player: the skill to unlock and whether it
    /// begins selected. Part of the <see cref="NewPlayer"/> blueprint.
    /// </summary>
    public class NewPlayerSkill
    {
        public required int SkillId { get; init; }

        public required bool Selected { get; init; }
    }
}
