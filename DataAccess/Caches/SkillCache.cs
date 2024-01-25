using DataAccess.Models.Skills;

namespace DataAccess.Caches
{
    internal class SkillCache : ISkillCache
    {
        private readonly List<Skill> _skillDataList;
        private readonly object _lock = new();

        public SkillCache(IRepositoryManager repositoryManager)
        {
            _skillDataList = repositoryManager.Skills.AllSkills();
        }

        public List<Skill> AllSkills()
        {
            return _skillDataList;
        }

        public Skill GetSkill(int skillId)
        {
            return _skillDataList[skillId];
        }
    }

    public interface ISkillCache
    {
        public List<Skill> AllSkills();
        public Skill GetSkill(int skillId);
    }
}
