using GameCore.Entities;

namespace GameCore.DataAccess
{
    public interface ISkills
    {
        public Task<IEnumerable<Skill>> AllSkillsAsync();
        public Task<Skill> GetSkillAsync(int skillId);
        public Task SaveSkillsAsync(List<int> skillIds);
    }
}
