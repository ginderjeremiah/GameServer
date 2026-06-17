using Contracts = Game.Abstractions.Contracts;
using CoreSkill = Game.Core.Skills.Skill;

namespace Game.Abstractions.DataAccess
{
    public interface ISkills
    {
        public List<Contracts.Skill> AllSkills();
        public CoreSkill GetSkill(int skillId);

        /// <inheritdoc cref="IItems.VersionKey"/>
        public object VersionKey { get; }
    }
}
