using Game.Core;
using Contracts = Game.Abstractions.Contracts;
using CorePath = Game.Core.Proficiencies.Path;
using CoreProficiency = Game.Core.Proficiencies.Proficiency;

namespace Game.Abstractions.DataAccess
{
    public interface IProficiencies
    {
        public List<Contracts.Proficiency> AllProficiencies();

        /// <summary>Every path — the reference set the tree screen renders.</summary>
        public List<Contracts.Path> AllPaths();

        public CoreProficiency GetProficiency(int proficiencyId);

        /// <summary>The XP-routing view of a path (its activity key and ordered tiers) the battle XP path
        /// resolves the frontier tier against.</summary>
        public CorePath GetPath(int pathId);

        /// <summary>The (non-retired) paths that train on <paramref name="activityKey"/>, or empty if none —
        /// the reverse index the battle XP path consumes to route a battle quantity to each path's frontier
        /// tier.</summary>
        public IReadOnlyList<CorePath> PathsForActivityKey(EActivityKey activityKey);

        /// <summary>The proficiencies that name <paramref name="proficiencyId"/> as a prerequisite (the
        /// cross-path gateways it gates), or empty if none — the reverse index the open logic consumes when a
        /// proficiency is maxed to resolve which gateways might now open.</summary>
        public IReadOnlyList<int> DependentsOf(int proficiencyId);

        /// <inheritdoc cref="IItems.VersionKey"/>
        public object VersionKey { get; }
    }
}
