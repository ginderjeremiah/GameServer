using Game.Abstractions.Entities;
using CoreSkill = Game.Core.Skills.Skill;

namespace Game.Abstractions.DataAccess
{
    public interface ISkills
    {
        public void InvalidateCache();
        public List<Skill> AllSkills(bool refreshCache = false);
        public Skill? LookupSkill(int skillId);
        public CoreSkill GetSkill(int skillId);
    }
}
