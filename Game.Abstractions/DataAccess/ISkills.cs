using Contracts = Game.Abstractions.Contracts;
using CoreSkill = Game.Core.Skills.Skill;

namespace Game.Abstractions.DataAccess
{
    public interface ISkills
    {
        public void InvalidateCache();
        public List<Contracts.Skill> AllSkills(bool refreshCache = false);
        public CoreSkill GetSkill(int skillId);
    }
}
