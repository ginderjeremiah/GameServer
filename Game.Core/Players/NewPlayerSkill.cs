namespace Game.Core.Players
{
    /// <summary>
    /// A starter skill granted to a freshly created player: the skill to unlock, whether it
    /// begins selected, and its position in the equipped loadout. Part of the
    /// <see cref="NewPlayer"/> blueprint.
    /// </summary>
    public class NewPlayerSkill
    {
        public required int SkillId { get; init; }

        public required bool Selected { get; init; }

        /// <summary>The skill's position in the equipped loadout (used as the persisted loadout order).</summary>
        public required int Order { get; init; }
    }
}
