using Contracts = Game.Abstractions.Contracts;
using CoreSkill = Game.Core.Skills.Skill;

namespace Game.Abstractions.DataAccess
{
    public interface ISkills
    {
        public List<Contracts.Skill> AllSkills();
        public CoreSkill GetSkill(int skillId);

        /// <summary>
        /// The lean <see cref="CoreSkill"/> for <paramref name="skillId"/>, or <c>null</c> when no skill has
        /// that id — the nullable companion to <see cref="GetSkill"/>. The battle-loadout weapon-match gate uses
        /// it so an unauthored optional grant (e.g. the bare-hands punch when no punch skill is seeded) is
        /// skipped rather than throwing.
        /// </summary>
        public CoreSkill? TryGetSkill(int skillId);

        /// <inheritdoc cref="IItems.VersionKey"/>
        public object VersionKey { get; }
    }
}
