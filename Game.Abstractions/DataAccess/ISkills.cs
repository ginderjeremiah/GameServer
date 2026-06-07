using Contracts = Game.Abstractions.Contracts;
using SkillEntity = Game.Abstractions.Entities.Skill;
using CoreSkill = Game.Core.Skills.Skill;

namespace Game.Abstractions.DataAccess
{
    public interface ISkills
    {
        public void InvalidateCache();
        public List<Contracts.Skill> AllSkills(bool refreshCache = false);
        // Returns the EF entity for the Content Authoring admin persistence (Game.DataAccess); the read path uses the contracts above.
        public SkillEntity? LookupSkill(int skillId);
        public CoreSkill GetSkill(int skillId);
    }
}
