using Game.Abstractions.Entities;

namespace Game.Abstractions.DataAccess
{
    public interface ISkills
    {
        public void InvalidateCache();
        public List<Skill> AllSkills(bool refreshCache = false);
        public Skill? GetSkill(int skillId);
        public Task SaveSkillsAsync(List<int> skillIds);
    }
}
