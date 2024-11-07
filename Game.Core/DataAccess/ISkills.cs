using Game.Core.Entities;

namespace Game.Core.DataAccess
{
    public interface ISkills
    {
        public List<Skill> AllSkills(bool refreshCache = false);
        public Skill? GetSkill(int skillId);
        public Task SaveSkillsAsync(List<int> skillIds);
    }
}
