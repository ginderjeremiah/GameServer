using Game.Core.Proficiencies;
using Contracts = Game.Abstractions.Contracts;
using CorePath = Game.Core.Proficiencies.Path;
using CoreProficiency = Game.Core.Proficiencies.Proficiency;

namespace Game.Abstractions.DataAccess
{
    public interface IProficiencies
    {
        public List<Contracts.Proficiency> AllProficiencies();

        /// <summary>Every path (with its skill contributions) — the reference set the tree screen renders.</summary>
        public List<Contracts.Path> AllPaths();

        public CoreProficiency GetProficiency(int proficiencyId);

        /// <summary>The XP-routing view of a path (its falloff base and ordered tiers) the battle XP path
        /// resolves a contribution's frontier tier and falloff against.</summary>
        public CorePath GetPath(int pathId);

        /// <summary>The contributions the given skill makes (each a path, home tier, and weight), or empty if
        /// none — the reverse index the battle XP path consumes.</summary>
        public IReadOnlyList<SkillContribution> ContributionsForSkill(int skillId);

        /// <inheritdoc cref="IItems.VersionKey"/>
        public object VersionKey { get; }
    }
}
