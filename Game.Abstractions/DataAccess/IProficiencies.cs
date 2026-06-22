using Game.Core.Proficiencies;
using Contracts = Game.Abstractions.Contracts;
using CoreProficiency = Game.Core.Proficiencies.Proficiency;

namespace Game.Abstractions.DataAccess
{
    public interface IProficiencies
    {
        public List<Contracts.Proficiency> AllProficiencies();

        /// <summary>Every path (with its skill contributions) — the reference set the tree screen renders.</summary>
        public List<Contracts.Path> AllPaths();

        public CoreProficiency GetProficiency(int proficiencyId);

        /// <summary>The proficiencies the given skill contributes to (with weights), or empty if none — the
        /// reverse index the battle XP path consumes.</summary>
        public IReadOnlyList<SkillContribution> ContributionsForSkill(int skillId);

        /// <inheritdoc cref="IItems.VersionKey"/>
        public object VersionKey { get; }
    }
}
